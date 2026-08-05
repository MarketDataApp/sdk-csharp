using System.Net;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Covers §9.5: the cached <c>/status/</c> gate applied before retrying a retryable server error.
/// All HTTP is mocked and time is driven by an injected <see cref="TimeProvider"/>, so every case
/// is deterministic — including the settling of the non-blocking background refresh, which is
/// observed by awaiting the mocked handler's <c>/status/</c> request signal.
/// </summary>
public sealed class StatusGateTests
{
    private const string StatusPath = "/status/";
    private const string StocksServicePath = "/v1/stocks/quotes/";
    private const string OptionsServicePath = "/v1/options/chain/";

    [Fact]
    public async Task OfflineCachedStatus_Fresh_ServerError_FailsImmediatelyWithoutRetry()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var handler = new StubHttpMessageHandler(request =>
            Respond(request, counters, online: false, dataResponse: ServerError));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        // Seed the shared cache with a fresh OFFLINE reading via an explicit status check.
        await client.Utilities.GetStatusAsync();

        var exception = await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(1, counters.DataAttempts);      // offline => no retry, single attempt
        Assert.Equal(1, counters.StatusRequests);    // fresh cache => no background refresh
    }

    [Fact]
    public async Task OnlineCachedStatus_Fresh_ServerError_RetriesPerPolicy()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var handler = new StubHttpMessageHandler(request =>
            Respond(request, counters, online: true, dataResponse: ServerError));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 2,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        // Seed the shared cache with a fresh ONLINE reading.
        await client.Utilities.GetStatusAsync();

        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        Assert.Equal(3, counters.DataAttempts);      // online => full retry schedule (1 + 2 retries)
        Assert.Equal(1, counters.StatusRequests);    // fresh cache => no background refresh
    }

    [Fact]
    public async Task EmptyCache_ServerError_RetriesAndTriggersNonBlockingRefresh()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(request =>
            Respond(request, counters, online: true, dataResponse: ServerError, statusRequested: refreshed));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 2,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        // No seeding: the cache is empty, so the status is UNKNOWN and retries must proceed.
        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        // Settle the fire-and-forget refresh deterministically by awaiting its /status/ request.
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(3, counters.DataAttempts);      // unknown => retries proceed
        Assert.True(counters.StatusRequests >= 1);   // a background refresh was triggered
    }

    [Fact]
    public async Task StaleWithinValidity_UsesCachedDecisionAndTriggersNonBlockingRefresh()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var secondStatusRequested =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(request => Respond(
            request,
            counters,
            online: false,
            dataResponse: ServerError,
            // Signal only when the refresh (the second /status/ call) reaches the handler.
            statusRequested: secondStatusRequested,
            signalAtStatusCount: 2));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        // Seed a fresh OFFLINE reading, then age it into the [270s, 300s) window.
        await client.Utilities.GetStatusAsync();
        time.Advance(TimeSpan.FromSeconds(280));

        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        await secondStatusRequested.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, counters.DataAttempts);      // cached (still-valid) offline decision blocks the retry
        Assert.Equal(2, counters.StatusRequests);    // seed + one background refresh
    }

    [Fact]
    public async Task NetworkError_RetriesRegardlessOfOfflineStatus()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var handler = new StubHttpMessageHandler(request =>
            Respond(request, counters, online: false, dataResponse: NetworkFailure));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 2,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        // Seed a fresh OFFLINE reading; it must NOT gate network-error retries.
        await client.Utilities.GetStatusAsync();

        await Assert.ThrowsAsync<NetworkException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        Assert.Equal(3, counters.DataAttempts);      // network errors keep retrying, ungated
        Assert.Equal(1, counters.StatusRequests);    // network path never consults or refreshes status
    }

    [Fact]
    public async Task CachedStatusForDifferentService_DoesNotBlockUnrelatedRetry()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        // Cache an OFFLINE reading for the OPTIONS service only.
        var handler = new StubHttpMessageHandler(request =>
            Respond(request, counters, online: false, dataResponse: ServerError, servicePath: OptionsServicePath));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        await client.Utilities.GetStatusAsync();

        // A STOCKS request has no matching cache entry => UNKNOWN => retries proceed.
        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        Assert.Equal(2, counters.DataAttempts);      // no-match => not blocked
        Assert.Equal(1, counters.StatusRequests);    // fresh cache => no background refresh
    }

    [Fact]
    public async Task RefreshFailure_IsSwallowed_AndRetriesStillProceed()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var counters = new Counters();
        var refreshAttempted =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Empty cache triggers a refresh; the /status/ fetch fails and must be swallowed.
        var handler = new StubHttpMessageHandler(request => Respond(
            request,
            counters,
            online: true,
            dataResponse: ServerError,
            statusRequested: refreshAttempted,
            statusResponse: NetworkFailure));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time
        });

        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        await refreshAttempted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(2, counters.DataAttempts);      // failed refresh => status unknown => retries proceed
        Assert.True(counters.StatusRequests >= 1);   // a background refresh was attempted
    }

    private static readonly Func<HttpResponseMessage> ServerError = () =>
        new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("temporary")
        };

    private static readonly Func<HttpResponseMessage> NetworkFailure = () =>
        throw new HttpRequestException("network down");

    // Serves the shared handler: /status/ returns the requested online/offline reading (and bumps
    // the status counter, optionally signalling once a target count is reached); every other path
    // returns the supplied data response.
    private static HttpResponseMessage Respond(
        HttpRequestMessage request,
        Counters counters,
        bool online,
        Func<HttpResponseMessage> dataResponse,
        TaskCompletionSource? statusRequested = null,
        int signalAtStatusCount = 1,
        string servicePath = StocksServicePath,
        Func<HttpResponseMessage>? statusResponse = null)
    {
        if (request.RequestUri!.AbsolutePath == StatusPath)
        {
            var count = counters.RecordStatusRequest();
            if (statusRequested is not null && count >= signalAtStatusCount)
            {
                statusRequested.TrySetResult();
            }

            return statusResponse is not null
                ? statusResponse()
                : MarketDataTestClient.JsonResponse(StatusJson(online, servicePath));
        }

        counters.RecordDataAttempt();
        return dataResponse();
    }

    private static string StatusJson(bool online, string servicePath)
    {
        var status = online ? "online" : "offline";
        var flag = online ? "true" : "false";
        return $$"""
        {
          "s": "ok",
          "service": ["{{servicePath}}"],
          "status": ["{{status}}"],
          "online": [{{flag}}],
          "uptimePct30d": [0.99],
          "uptimePct90d": [0.98],
          "updated": [1706745600]
        }
        """;
    }

    private sealed class Counters
    {
        private int _dataAttempts;
        private int _statusRequests;

        public int DataAttempts => Volatile.Read(ref _dataAttempts);
        public int StatusRequests => Volatile.Read(ref _statusRequests);

        public void RecordDataAttempt() => Interlocked.Increment(ref _dataAttempts);
        public int RecordStatusRequest() => Interlocked.Increment(ref _statusRequests);
    }

    // Time source that reports a settable UtcNow and can be advanced deterministically. The base
    // CreateTimer (real time) still backs the request-timeout CTS, which never fires because the
    // mocked handler responds instantly.
    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private long _ticks = start.UtcTicks;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        public void Advance(TimeSpan delta) => Interlocked.Add(ref _ticks, delta.Ticks);
    }
}

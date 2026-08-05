using System.Net;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Coverage-completing tests for <see cref="ApiClient"/> reached through the public
/// <see cref="MarketDataClient"/> surface: constructor option validation, HTTP error-status
/// mappings, resource disposal, and startup token-redaction logging.
/// </summary>
public sealed class ApiClientCoverageTests
{
    private static MarketDataClient Create(HttpMessageHandler handler, MarketDataClientOptions options) =>
        new(new HttpClient(handler), options);

    private static StubHttpMessageHandler NoRequest() =>
        new(_ => throw new InvalidOperationException("No request expected."));

    [Fact]
    public void Constructor_RejectsInvalidTransportOptions()
    {
        using var handler = NoRequest();

        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { BaseAddress = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { MaxRetries = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { RetryBaseDelay = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { RetryMaxDelay = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { MaxRetryAfter = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { RetryJitterFactor = -0.1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { RetryJitterFactor = 1.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { MaxConcurrentRequests = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(handler, new MarketDataClientOptions { MaxConcurrentRequests = 51 }));
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { TimeProvider = null! }));
    }

    [Fact]
    public void Constructor_RejectsInvalidApiVersionTokenAndUserAgent()
    {
        using var handler = NoRequest();

        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { ApiVersion = "v 1" }));
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { ApiToken = "token" }));
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { UserAgent = "   " }));
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { UserAgent = "bad\r\nagent" }));
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { ApiVersion = "  " }));
    }

    [Fact]
    public void Constructor_AcceptsApiVersionWithPunctuationCharacters()
    {
        using var handler = NoRequest();

        // Periods, underscores, and hyphens are permitted in the version segment.
        var client = Create(handler, new MarketDataClientOptions { ApiVersion = "v1.2_3-4" });

        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public async Task StartupValidationFailure_IsLoggedWhenLoggerAttached()
    {
        var logger = new CapturingLogger();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("invalid token")
        });

        await Assert.ThrowsAsync<AuthenticationException>(() => MarketDataClient.CreateAsync(
            new HttpClient(handler),
            new MarketDataClientOptions
            {
                ApiToken = "secret-token",
                Logger = logger,
                MinimumLogLevel = LogLevel.Debug
            }));

        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Error && e.Message.Contains("Startup token validation failed"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(BadRequestException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(AuthenticationException))]
    public async Task ErrorStatuses_MapToSpecificExceptions(HttpStatusCode status, Type exceptionType)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("error detail")
        });
        var client = Create(handler, new MarketDataClientOptions { MaxRetries = 0 });

        var exception = await Assert.ThrowsAsync(exceptionType,
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        Assert.Contains("error detail", exception.Message);
    }

    [Fact]
    public async Task UnmappedErrorStatus_MapsToBaseMarketDataException()
    {
        // 402 Payment Required has no dedicated subtype, so it maps to the base MarketDataException.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.PaymentRequired)
        {
            Content = new StringContent("payment required")
        });
        var client = Create(handler, new MarketDataClientOptions { MaxRetries = 0 });

        var exception = await Assert.ThrowsAnyAsync<MarketDataException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        Assert.Equal(402, exception.StatusCode);
        Assert.Contains("payment required", exception.Message);
    }

    [Fact]
    public async Task EmptyErrorBody_UsesStatusOnlyMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = Create(handler, new MarketDataClientOptions { MaxRetries = 0 });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        Assert.Equal("The Market Data API returned HTTP 400.", exception.Message);
    }

    [Fact]
    public async Task Dispose_ReleasesResources_AndSupportsDefaultConstruction()
    {
        var handler = new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}"""));

        // Constructor with no options falls back to FromEnvironment (demo mode, no network I/O).
        var client = new MarketDataClient(new HttpClient(handler));
        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));
        client.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithoutOptions_UsesEnvironmentAndSkipsValidationInDemoMode()
    {
        var requests = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = await MarketDataClient.CreateAsync(new HttpClient(handler));

        Assert.Equal(0, requests);
        Assert.Null(client.LatestRateLimit);
    }

    [Fact]
    public void StartupLogging_RedactsConfiguredTokenSuffix()
    {
        var logger = new CapturingLogger();
        _ = new MarketDataClient(
            new HttpClient(NoRequest()),
            new MarketDataClientOptions
            {
                ApiToken = "secret-token-1234",
                Logger = logger,
                MinimumLogLevel = LogLevel.Debug
            });

        var tokenLog = Assert.Single(
            logger.Entries,
            e => e.Level == LogLevel.Debug && e.Message.Contains("redacted suffix"));
        Assert.Contains("****1234", tokenLog.Message);
        Assert.DoesNotContain("secret-token", tokenLog.Message);
    }

    [Fact]
    public void StartupLogging_RedactsShortTokenEntirely()
    {
        var logger = new CapturingLogger();
        _ = new MarketDataClient(
            new HttpClient(NoRequest()),
            new MarketDataClientOptions
            {
                ApiToken = "ab",
                Logger = logger,
                MinimumLogLevel = LogLevel.Debug
            });

        var tokenLog = Assert.Single(
            logger.Entries,
            e => e.Level == LogLevel.Debug && e.Message.Contains("redacted suffix"));
        Assert.Contains("****", tokenLog.Message);
        Assert.DoesNotContain("ab", tokenLog.Message);
    }
}

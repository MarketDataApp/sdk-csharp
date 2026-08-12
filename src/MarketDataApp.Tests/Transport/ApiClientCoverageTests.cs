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
/// Member of <see cref="DotEnvCwdCollection"/>: two tests construct clients without options,
/// which reads the CWD .env via FromEnvironment and must never overlap the .env fixtures.
/// </summary>
[Collection(DotEnvCwdCollection.Name)]
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
        // A character above '~' (0x7E) is also non-printable ASCII and rejected.
        Assert.Throws<ArgumentException>(() =>
            Create(handler, new MarketDataClientOptions { ApiToken = "tokén" }));
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
    public void CreateDefaultHttpHandler_AppliesTwoSecondConnectTimeout()
    {
        using var handler = MarketDataClient.CreateDefaultHttpHandler();

        // §10: the caller-owned handler supplies the separate 2-second connection timeout while the
        // SDK enforces the fixed 99-second request timeout internally.
        Assert.Equal(TimeSpan.FromSeconds(2), handler.ConnectTimeout);
        Assert.True(handler.PooledConnectionLifetime > TimeSpan.Zero);
        Assert.True(handler.PooledConnectionIdleTimeout > TimeSpan.Zero);
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

    [Fact]
    public void BaseAddress_ValidatesUriShape()
    {
        using var handler = NoRequest();

        // A relative URI is not absolute and is rejected before the scheme is inspected.
        Assert.Throws<ArgumentException>(() => Create(handler, new MarketDataClientOptions
        {
            BaseAddress = new Uri("relative/path", UriKind.Relative)
        }));
        // A fragment is rejected.
        Assert.Throws<ArgumentException>(() => Create(handler, new MarketDataClientOptions
        {
            BaseAddress = new Uri("https://api.marketdata.app/#frag")
        }));
        // Plain http (as well as https) is an accepted scheme.
        var client = Create(handler, new MarketDataClientOptions
        {
            BaseAddress = new Uri("http://api.marketdata.app/")
        });
        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public async Task PartialRateLimitHeaders_LeaveSnapshotUnset()
    {
        // limit present but remaining missing: ParseRateLimit short-circuits and records nothing.
        await AssertNoRateLimitSnapshotAsync(new Dictionary<string, string>
        {
            ["x-api-ratelimit-limit"] = "100",
            ["x-api-ratelimit-reset"] = "1737072000",
            ["x-api-ratelimit-consumed"] = "1"
        });
        // limit + remaining present but reset missing: short-circuits at the reset read.
        await AssertNoRateLimitSnapshotAsync(new Dictionary<string, string>
        {
            ["x-api-ratelimit-limit"] = "100",
            ["x-api-ratelimit-remaining"] = "99",
            ["x-api-ratelimit-consumed"] = "1"
        });
    }

    private static async Task AssertNoRateLimitSnapshotAsync(IDictionary<string, string> headers)
    {
        var handler = new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}"""));
        foreach (var header in headers)
        {
            handler.ResponseHeaders[header.Key] = header.Value;
        }

        var client = MarketDataTestClient.Create(handler);
        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        Assert.Null(client.LatestRateLimit);
    }

    [Fact]
    public async Task ZeroLimitSnapshot_DoesNotBlockPreflight()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}""");
        });
        handler.ResponseHeaders["x-api-ratelimit-limit"] = "0";
        handler.ResponseHeaders["x-api-ratelimit-remaining"] = "0";
        handler.ResponseHeaders["x-api-ratelimit-reset"] =
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString();
        handler.ResponseHeaders["x-api-ratelimit-consumed"] = "0";
        var client = MarketDataTestClient.Create(handler);

        // First request seeds a snapshot whose Limit is 0 (treated as unlimited/unknown).
        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));
        // A Limit==0 snapshot must not trip the exhausted-rate-limit preflight, so the second is sent.
        await client.Stocks.GetPricesAsync(new StockPricesRequest("MSFT"));

        Assert.Equal(2, attempts);
        Assert.Equal(0, client.LatestRateLimit!.Limit);
    }
}

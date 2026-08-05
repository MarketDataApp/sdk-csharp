using System.Net;
using System.Diagnostics;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Coverage-completing tests for <see cref="StatusGate"/>: service-key extraction for rootless and
/// version-only status rows, and the Debug diagnostic emitted when a background refresh fails.
/// </summary>
public sealed class StatusGateCoverageTests
{
    private static readonly Func<HttpResponseMessage> ServerError = () =>
        new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("temporary")
        };

    [Fact]
    public async Task Evaluate_SkipsRootlessAndVersionOnlyStatusRows()
    {
        var time = new FrozenTimeProvider(DateTimeOffset.UnixEpoch);
        var dataAttempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/status/")
            {
                // Rows "/" (0 segments) and "/v1/" (version only) both map to a null service key
                // and are skipped; the real stocks row is offline and blocks the retry.
                return MarketDataTestClient.JsonResponse("""
                {
                  "s": "ok",
                  "service": ["/", "/v1/", "/v1/stocks/quotes/"],
                  "status": ["offline", "offline", "offline"],
                  "online": [false, false, false],
                  "uptimePct30d": [0.9, 0.9, 0.9],
                  "uptimePct90d": [0.9, 0.9, 0.9],
                  "updated": [1706745600, 1706745600, 1706745600]
                }
                """);
            }

            Interlocked.Increment(ref dataAttempts);
            return ServerError();
        });
        var logger = new CapturingLogger();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.Zero,
            TimeProvider = time,
            Logger = logger
        });

        // Seed a fresh OFFLINE reading that includes the rootless/version-only rows.
        await client.Utilities.GetStatusAsync();

        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));

        // The offline stocks row blocks the retry: exactly one data attempt.
        Assert.Equal(1, dataAttempts);
        // With a logger attached, the "skipping retry" warning is emitted (Warning >= Information).
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Skipping retry"));
    }

    [Fact]
    public async Task BackgroundRefreshFailure_LogsDebugDiagnostic_WhenLoggerAttached()
    {
        var logger = new CapturingLogger();
        var statusRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/status/")
            {
                statusRequested.TrySetResult();
                throw new HttpRequestException("status endpoint down");
            }

            return ServerError();
        });
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.Zero,
            Logger = logger,
            MinimumLogLevel = LogLevel.Debug
        });

        // Empty cache => the first retryable 5xx triggers a background /status/ refresh, which fails.
        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        await statusRequested.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await WaitForAsync(() => logger.Entries.Any(e =>
            e.Level == LogLevel.Debug && e.Message.Contains("Background /status/ refresh failed")));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected background-refresh Debug log was not emitted.");
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Focused tests for <see cref="ApiClient"/>'s retry-delay computation now that it is measured (no
/// longer excluded from coverage): the exponential backoff, the jittered-backoff computation and its
/// clamp to <c>RetryMaxDelay</c>, the overflow guard, and the server <c>Retry-After</c> path. The
/// applied delay is observed through the <c>marketdata.retry</c> activity's <c>delay_ms</c> tag; a
/// deterministic jitter sampler is injected via
/// <see cref="MarketDataClientOptions.RetryJitterSource"/> so the jitter branches are exact and the
/// tests never depend on <see cref="Random.Shared"/>.
/// </summary>
public sealed class RetryDelayTests
{
    public RetryDelayTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    [Fact]
    public async Task NetworkFailure_RetriesUsingExponentialBackoffPath()
    {
        // A NetworkException is retryable and takes the non-ServerException switch arm (no
        // Retry-After), so RetryDelay falls through to the exponential-backoff branch.
        var handler = new NetworkThenSuccessHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.Zero
        });

        var response = await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        Assert.Equal(2, handler.Attempts);
        Assert.False(response.IsNoData);
    }

    [Fact]
    public async Task ServerRetryAfterZero_ProducesImmediateRetry()
    {
        // A ServerException with Retry-After == 0 exercises the server-delay arm and the
        // "serverDelay <= Zero" branch, which returns TimeSpan.Zero (an immediate retry).
        var attempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/status/")
            {
                return MarketDataTestClient.JsonResponse("""{"s":"no_data"}""");
            }

            attempts++;
            if (attempts == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("temporary")
                };
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            }

            return MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}""");
        });
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions { MaxRetries = 1 });

        var response = await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        Assert.Equal(2, attempts);
        Assert.False(response.IsNoData);
    }

    [Fact]
    public async Task JitteredBackoff_StaysWithinConfiguredInterval()
    {
        const double jitterFactor = 0.5;
        var baseDelay = TimeSpan.FromSeconds(4);
        var delayMs = await CaptureFirstRetryDelayMsAsync(new MarketDataClientOptions
        {
            MaxRetries = 5,
            RetryBaseDelay = baseDelay,
            RetryMaxDelay = TimeSpan.FromSeconds(30),
            RetryJitterFactor = jitterFactor,
            RetryJitterSource = () => 0.5, // midpoint sample -> jitter multiplier 1.0
        });

        // First retry: retryCount 0 -> backoff base == 4s; the jittered delay must land within
        // [base*(1-j), base*(1+j)] and below RetryMaxDelay (so the clamp branch is not taken).
        Assert.InRange(
            delayMs,
            baseDelay.TotalMilliseconds * (1 - jitterFactor),
            baseDelay.TotalMilliseconds * (1 + jitterFactor));
        Assert.Equal(baseDelay.TotalMilliseconds, delayMs, 3);
    }

    [Fact]
    public async Task JitteredBackoff_IsClampedToRetryMaxDelay()
    {
        var maxDelay = TimeSpan.FromSeconds(30);
        var delayMs = await CaptureFirstRetryDelayMsAsync(new MarketDataClientOptions
        {
            MaxRetries = 5,
            RetryBaseDelay = maxDelay, // base == max -> backoff already at the ceiling
            RetryMaxDelay = maxDelay,
            RetryJitterFactor = 0.5,
            RetryJitterSource = () => 1.0, // max sample -> jitter multiplier 1.5 -> exceeds ceiling
        });

        // 30s * 1.5 == 45s exceeds RetryMaxDelay, so the applied delay is clamped back to 30s.
        Assert.Equal(maxDelay.TotalMilliseconds, delayMs, 3);
    }

    [Fact]
    public async Task ExponentialBackoff_WithoutJitter_UsesBaseDelay()
    {
        var baseDelay = TimeSpan.FromSeconds(4);
        var delayMs = await CaptureFirstRetryDelayMsAsync(new MarketDataClientOptions
        {
            MaxRetries = 5,
            RetryBaseDelay = baseDelay,
            RetryMaxDelay = TimeSpan.FromSeconds(30),
            RetryJitterFactor = 0, // no jitter -> delay is exactly the backoff base on the first retry
        });

        Assert.Equal(baseDelay.TotalMilliseconds, delayMs, 3);
    }

    [Fact]
    public void OverflowGuard_ClampsBackoffToRetryMaxDelay()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        using var apiClient = new ApiClient(httpClient, new MarketDataClientOptions
        {
            ValidateTokenOnStartup = false,
            RetryBaseDelay = TimeSpan.MaxValue,
            RetryMaxDelay = TimeSpan.MaxValue,
            RetryJitterFactor = 0,
        });
        var exception = new ServerException(
            "boom",
            ErrorContext.ForNoResponse(
                new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
                DateTimeOffset.UnixEpoch));

        // retryCount 1 -> multiplier 2 -> base.Ticks * 2 would overflow Int64, so the guard clamps
        // the backoff to RetryMaxDelay instead of wrapping to a negative/garbage delay.
        var delay = apiClient.RetryDelay(exception, retryCount: 1);

        Assert.Equal(TimeSpan.MaxValue, delay);
    }

    /// <summary>
    /// Drives a single retry against an always-5xx endpoint and returns the delay (in ms) the
    /// transport applied, read from the <c>marketdata.retry</c> activity. The retry's tags are set
    /// immediately before the backoff <c>Task.Delay</c>, so the request is cancelled once the retry
    /// activity has started; the delay is then read from the activity on stop without any real wait.
    /// The captured activity is correlated to this call by a scoped root activity's trace id so
    /// concurrently-running test classes cannot cross-contaminate the result.
    /// </summary>
    private static async Task<double> CaptureFirstRetryDelayMsAsync(MarketDataClientOptions options)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/status/")
            {
                return MarketDataTestClient.JsonResponse("""{"s":"no_data"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporary"),
            };
        });
        using var client = MarketDataTestClient.Create(handler, options);

        using var root = new Activity("retry-delay-test").Start();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MarketDataDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                if (activity.TraceId == root.TraceId && activity.OperationName == "marketdata.retry")
                {
                    started.TrySetResult();
                }
            },
            ActivityStopped = activity =>
            {
                if (activity.TraceId == root.TraceId
                    && activity.OperationName == "marketdata.retry"
                    && activity.GetTagItem("marketdata.retry.delay_ms") is double ms)
                {
                    captured.TrySetResult(ms);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var cancellation = new CancellationTokenSource();
        var request = client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL"),
            cancellationToken: cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);

        return await captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    // Simulates a transient network fault on the first attempt and succeeds afterward, so the retry
    // loop maps the first send to a NetworkException.
    private sealed class NetworkThenSuccessHandler : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                throw new HttpRequestException("connection reset");
            }

            return MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}""");
        }
    }
}

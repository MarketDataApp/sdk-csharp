using System.Diagnostics;
using System.Net;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Verifies the OpenTelemetry <see cref="Activity"/> instrumentation emitted by the transport: HTTP
/// request tags, retry-delay tags, and error status/exception recording. Each test that needs live
/// activities registers a scoped <see cref="ActivityListener"/> for its own duration so
/// <c>ActivitySource.StartActivity</c> returns real activities (exercising the non-null
/// <c>activity?.SetTag/SetStatus/AddException</c> branches). The error-path tests that need
/// <c>activity == null</c> register no listener; because xunit runs a class's tests serially and no
/// other test class registers a listener, those runs deterministically see no activity. Captured
/// activities are correlated to each test by a scoped root activity's trace id.
/// </summary>
public sealed class TelemetryTests
{
    public TelemetryTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    private static ActivityListener CreateListener(List<Activity> sink) =>
        new()
        {
            ShouldListenTo = source => source.Name == MarketDataDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (sink)
                {
                    sink.Add(activity);
                }
            }
        };

    private static Activity SingleActivity(List<Activity> sink, ActivityTraceId traceId, string operationName)
    {
        lock (sink)
        {
            return Assert.Single(sink, a => a.TraceId == traceId && a.OperationName == operationName);
        }
    }

    [Fact]
    public async Task SuccessfulRequest_RecordsHttpActivityTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);
        ActivitySource.AddActivityListener(listener);

        var handler = new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}"""));
        handler.ResponseHeaders["cf-ray"] = "ray-123";
        var client = MarketDataTestClient.Create(handler);

        using var root = new Activity("test").Start();
        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        var http = SingleActivity(captured, root.TraceId, "marketdata.http.get");
        Assert.Equal("GET", http.GetTagItem("http.request.method"));
        Assert.Equal(200, http.GetTagItem("http.response.status_code"));
        Assert.Equal("ray-123", http.GetTagItem("marketdata.request_id"));
        Assert.NotNull(http.GetTagItem("url.full"));
    }

    [Fact]
    public async Task RetriedRequest_RecordsRetryActivityTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);
        ActivitySource.AddActivityListener(listener);

        var attempts = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/status/")
            {
                return MarketDataTestClient.JsonResponse("""{"s":"no_data"}""");
            }

            attempts++;
            return attempts < 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("temporary")
                }
                : MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}""");
        });
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.Zero
        });

        using var root = new Activity("test").Start();
        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        var retry = SingleActivity(captured, root.TraceId, "marketdata.retry");
        Assert.Equal(1, retry.GetTagItem("marketdata.retry.count"));
        Assert.Equal("ServerException", retry.GetTagItem("error.type"));
        Assert.NotNull(retry.GetTagItem("marketdata.retry.delay_ms"));
    }

    [Fact]
    public async Task NetworkError_RecordsErrorStatusAndException()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);
        ActivitySource.AddActivityListener(listener);

        var client = MarketDataTestClient.Create(new ThrowingHandler(),
            new MarketDataClientOptions { MaxRetries = 0 });

        using var root = new Activity("test").Start();
        await Assert.ThrowsAsync<NetworkException>(
            () => client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL")));

        var http = SingleActivity(captured, root.TraceId, "marketdata.http.get");
        Assert.Equal(ActivityStatusCode.Error, http.Status);
        Assert.Contains(http.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task Timeout_RecordsTimeoutStatusAndException()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);
        ActivitySource.AddActivityListener(listener);

        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var handler = new HangingHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 0,
            TimeProvider = time
        });

        using var root = new Activity("test").Start();
        var request = client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));
        await handler.Started;
        time.Advance(TimeSpan.FromSeconds(100));

        await Assert.ThrowsAsync<NetworkException>(() => request);

        var http = SingleActivity(captured, root.TraceId, "marketdata.http.get");
        Assert.Equal(ActivityStatusCode.Error, http.Status);
        Assert.Equal("timeout", http.StatusDescription);
        Assert.Contains(http.Events, e => e.Name == "exception");
    }

    // The following two tests register NO listener. Because xunit runs this class's tests serially
    // and no other test class registers a listener, StartActivity returns null, exercising the
    // activity == null branch of the transport's error-recording code.

    [Fact]
    public async Task NetworkError_WithoutListener_ExercisesNullActivityBranch()
    {
        var client = MarketDataTestClient.Create(new ThrowingHandler(),
            new MarketDataClientOptions { MaxRetries = 0 });

        var exception = await Assert.ThrowsAsync<NetworkException>(
            () => client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL")));
        Assert.Equal(0, exception.StatusCode);
        Assert.Contains("could not be sent", exception.Message);
    }

    [Fact]
    public async Task Timeout_WithoutListener_ExercisesNullActivityBranch()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var handler = new HangingHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            MaxRetries = 0,
            TimeProvider = time
        });

        var request = client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));
        await handler.Started;
        time.Advance(TimeSpan.FromSeconds(100));

        var exception = await Assert.ThrowsAsync<NetworkException>(() => request);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

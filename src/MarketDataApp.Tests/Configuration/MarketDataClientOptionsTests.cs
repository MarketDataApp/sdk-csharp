using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Configuration;

public sealed class MarketDataClientOptionsTests
{
    [Fact]
    public void FromConfiguration_BindsTransportSettings()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MARKETDATA_TOKEN"] = "token",
            ["MARKETDATA_BASE_URL"] = "https://example.test/api/",
            ["MARKETDATA_API_VERSION"] = "v2",
            ["MARKETDATA_MAX_RETRIES"] = "4",
            ["MARKETDATA_RETRY_BASE_DELAY"] = "00:00:00.100",
            ["MARKETDATA_RETRY_MAX_DELAY"] = "00:00:10",
            ["MARKETDATA_MAX_RETRY_AFTER"] = "00:02:00",
            ["MARKETDATA_RETRY_JITTER_FACTOR"] = "0.1",
            ["MARKETDATA_MAX_CONCURRENT_REQUESTS"] = "12",
            ["MARKETDATA_USER_AGENT"] = "test-client/1.0"
        });

        var options = MarketDataClientOptions.FromConfiguration(configuration);

        Assert.Equal("token", options.ApiToken);
        Assert.Equal(new Uri("https://example.test/api/"), options.BaseAddress);
        Assert.Equal("v2", options.ApiVersion);
        Assert.Equal(4, options.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), options.RetryMaxDelay);
        Assert.Equal(TimeSpan.FromMinutes(2), options.MaxRetryAfter);
        Assert.Equal(0.1, options.RetryJitterFactor);
        Assert.Equal(12, options.MaxConcurrentRequests);
        Assert.Equal("test-client/1.0", options.UserAgent);
    }

    [Fact]
    public void FromConfiguration_RejectsMalformedValues()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MARKETDATA_MAX_RETRIES"] = "many"
        });

        var exception = Assert.Throws<FormatException>(
            () => MarketDataClientOptions.FromConfiguration(configuration));

        Assert.Contains("MARKETDATA_MAX_RETRIES", exception.Message);
    }

    [Fact]
    public void Client_RejectsInvalidConfiguredRanges()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?>
            {
                ["MARKETDATA_RETRY_BASE_DELAY"] = "00:00:10",
                ["MARKETDATA_RETRY_MAX_DELAY"] = "00:00:01"
            }));
        using var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException());

        Assert.Throws<ArgumentException>(() => MarketDataTestClient.Create(handler, options));
    }

    [Fact]
    public void FromConfiguration_BindsAllTransportTuningOptions()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?>
            {
                ["MARKETDATA_RETRY_BASE_DELAY"] = "00:00:00.100",
                ["MARKETDATA_RETRY_MAX_DELAY"] = "00:00:10",
                ["MARKETDATA_MAX_RETRY_AFTER"] = "00:02:00",
                ["MARKETDATA_RETRY_JITTER_FACTOR"] = "0.1",
                ["MARKETDATA_MAX_CONCURRENT_REQUESTS"] = "12",
                ["MARKETDATA_USER_AGENT"] = "test-client/1.0"
            }));

        Assert.Equal(TimeSpan.FromMilliseconds(100), options.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), options.RetryMaxDelay);
        Assert.Equal(TimeSpan.FromMinutes(2), options.MaxRetryAfter);
        Assert.Equal(0.1, options.RetryJitterFactor);
        Assert.Equal(12, options.MaxConcurrentRequests);
        Assert.Equal("test-client/1.0", options.UserAgent);
    }

    [Fact]
    public void FromConfiguration_BindsFormattingDefaults()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?>
            {
                ["MARKETDATA_DATE_FORMAT"] = "timestamp",
                ["MARKETDATA_MODE"] = "cached",
                ["MARKETDATA_COLUMNS"] = "symbol, mid , last",
                ["MARKETDATA_ADD_HEADERS"] = "false",
                ["MARKETDATA_USE_HUMAN_READABLE"] = "true",
                ["MARKETDATA_LOGGING_LEVEL"] = "DEBUG",
                ["MARKETDATA_OUTPUT_FORMAT"] = "csv"
            }));

        Assert.Equal(DateFormat.Timestamp, options.DefaultDateFormat);
        Assert.Equal(Mode.Cached, options.DefaultMode);
        Assert.Equal(new[] { "symbol", "mid", "last" }, options.DefaultColumns);
        Assert.False(options.DefaultAddHeaders);
        Assert.True(options.DefaultHuman);
        Assert.Equal(LogLevel.Debug, options.MinimumLogLevel);
        Assert.Equal(OutputFormat.Csv, options.OutputFormat);
    }

    [Fact]
    public void FromConfiguration_LeavesFormattingDefaultsUnsetWhenKeysAbsent()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?>()));

        Assert.Null(options.DefaultDateFormat);
        Assert.Null(options.DefaultMode);
        Assert.Null(options.DefaultColumns);
        Assert.Null(options.DefaultAddHeaders);
        Assert.Null(options.DefaultHuman);
        Assert.Null(options.OutputFormat);
        // MinimumLogLevel keeps its Information default so Debug SDK logs stay suppressed.
        Assert.Equal(LogLevel.Information, options.MinimumLogLevel);
    }

    [Theory]
    [InlineData("unix", DateFormat.Unix)]
    [InlineData("timestamp", DateFormat.Timestamp)]
    [InlineData("spreadsheet", DateFormat.Spreadsheet)]
    [InlineData("TIMESTAMP", DateFormat.Timestamp)]
    public void FromConfiguration_MapsDateFormat(string configured, DateFormat expected)
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_DATE_FORMAT"] = configured }));

        Assert.Equal(expected, options.DefaultDateFormat);
    }

    [Theory]
    [InlineData("live", Mode.Live)]
    [InlineData("delayed", Mode.Delayed)]
    [InlineData("cached", Mode.Cached)]
    [InlineData("CACHED", Mode.Cached)]
    public void FromConfiguration_MapsMode(string configured, Mode expected)
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_MODE"] = configured }));

        Assert.Equal(expected, options.DefaultMode);
    }

    [Theory]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("information", LogLevel.Information)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("Critical", LogLevel.Critical)]
    [InlineData("None", LogLevel.None)]
    public void FromConfiguration_MapsLoggingLevel(string configured, LogLevel expected)
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_LOGGING_LEVEL"] = configured }));

        Assert.Equal(expected, options.MinimumLogLevel);
    }

    [Theory]
    [InlineData("MARKETDATA_DATE_FORMAT", "iso")]
    [InlineData("MARKETDATA_MODE", "realtime")]
    [InlineData("MARKETDATA_LOGGING_LEVEL", "verbose")]
    [InlineData("MARKETDATA_OUTPUT_FORMAT", "xml")]
    [InlineData("MARKETDATA_ADD_HEADERS", "maybe")]
    [InlineData("MARKETDATA_USE_HUMAN_READABLE", "sometimes")]
    public void FromConfiguration_RejectsMalformedFormattingValues(string key, string value)
    {
        var configuration = Configuration(new Dictionary<string, string?> { [key] = value });

        var exception = Assert.Throws<FormatException>(
            () => MarketDataClientOptions.FromConfiguration(configuration));

        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void FromConfiguration_SplitsColumnsAndTrimsBlankEntries()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_COLUMNS"] = " symbol , ,mid ,, last " }));

        Assert.Equal(new[] { "symbol", "mid", "last" }, options.DefaultColumns);
    }

    [Fact]
    public void FromConfiguration_TreatsSeparatorOnlyColumnsAsUnset()
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_COLUMNS"] = ",,," }));

        Assert.Null(options.DefaultColumns);
    }

    [Theory]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("csv", OutputFormat.Csv)]
    [InlineData("CSV", OutputFormat.Csv)]
    public void FromConfiguration_MapsOutputFormat(string configured, OutputFormat expected)
    {
        var options = MarketDataClientOptions.FromConfiguration(Configuration(
            new Dictionary<string, string?> { ["MARKETDATA_OUTPUT_FORMAT"] = configured }));

        Assert.Equal(expected, options.OutputFormat);
    }

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}

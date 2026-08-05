using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Configuration;

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

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}

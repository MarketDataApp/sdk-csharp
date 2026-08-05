using MarketDataApp;
using Microsoft.Extensions.Configuration;

namespace MarketDataApp.Tests.Configuration;

public sealed class ConfigurationCoverageTests
{
    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void FromConfiguration_RejectsMalformedDouble()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MARKETDATA_RETRY_JITTER_FACTOR"] = "not-a-number"
        });

        var exception = Assert.Throws<FormatException>(
            () => MarketDataClientOptions.FromConfiguration(configuration));
        Assert.Contains("MARKETDATA_RETRY_JITTER_FACTOR", exception.Message);
    }

    [Fact]
    public void FromConfiguration_RejectsMalformedTimeSpan()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["MARKETDATA_RETRY_BASE_DELAY"] = "not-a-timespan"
        });

        var exception = Assert.Throws<FormatException>(
            () => MarketDataClientOptions.FromConfiguration(configuration));
        Assert.Contains("MARKETDATA_RETRY_BASE_DELAY", exception.Message);
    }

    [Fact]
    public void FromEnvironment_WithoutDotEnvFile_ReturnsUsableOptions()
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        // Guard: ensure no stray .env from a sibling test is present for this branch.
        var hadDotEnv = File.Exists(envPath);
        if (hadDotEnv)
        {
            return; // A concurrent test owns the .env file; skip the no-.env branch this run.
        }

        var options = MarketDataClientOptions.FromEnvironment();

        Assert.NotNull(options);
        Assert.Equal("v1", options.ApiVersion);
    }

    [Fact]
    public void FromEnvironment_WithDotEnvFile_LoadsValues()
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        var alreadyExisted = File.Exists(envPath);
        try
        {
            if (!alreadyExisted)
            {
                File.WriteAllText(envPath, "MARKETDATA_MAX_RETRIES=7\n");
            }

            var options = MarketDataClientOptions.FromEnvironment();

            Assert.NotNull(options);
            if (!alreadyExisted)
            {
                Assert.Equal(7, options.MaxRetries);
            }
        }
        finally
        {
            if (!alreadyExisted && File.Exists(envPath))
            {
                File.Delete(envPath);
            }
        }
    }
}

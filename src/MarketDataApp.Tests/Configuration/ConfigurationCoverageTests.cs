using MarketDataApp;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Configuration;

namespace MarketDataApp.Tests.Configuration;

[Collection(DotEnvCwdCollection.Name)]
public sealed class ConfigurationCoverageTests
{
    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void FromEnvironment_WithoutDotEnvFile_ReturnsUsableOptions()
    {
        // Serialized via DotEnvCwdCollection, so no sibling test owns .env while this runs;
        // the delete only clears a leftover from an aborted earlier run (no-op otherwise).
        File.Delete(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        var options = MarketDataClientOptions.FromEnvironment();

        Assert.NotNull(options);
        Assert.Equal("v1", options.ApiVersion);
    }

    [Fact]
    public void FromEnvironment_WithDotEnvFile_LoadsValues()
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        var tempPath = envPath + ".tmp";
        try
        {
            // Write-then-move: a rename is atomic on the same volume, so no reader can ever
            // observe a half-written .env even if a future test escapes the collection.
            File.WriteAllText(tempPath, "MARKETDATA_MAX_RETRIES=7\n");
            File.Move(tempPath, envPath, overwrite: true);

            var options = MarketDataClientOptions.FromEnvironment();

            Assert.Equal(7, options.MaxRetries);
        }
        finally
        {
            File.Delete(envPath);
            File.Delete(tempPath);
        }
    }
}

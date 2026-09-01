using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;

namespace MarketDataApp.IntegrationTests;

internal static class IntegrationTestConfiguration
{
    public static IConfiguration Instance { get; } = BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        // The library resolves .env from the working directory, which is where an
        // application runs from. A test assembly runs from its output directory, so the
        // repo's .env is picked up next to the binaries here, not in the library.
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Lowest precedence on purpose: a real environment variable still wins.
                ["MARKETDATA_MAX_RETRIES"] = "1"
            });

        var environmentFile = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(environmentFile))
        {
            builder.AddDotNetEnv(environmentFile, new LoadOptions(clobberExistingVars: false));
        }

        return builder
            .AddConfiguration(MarketDataClientOptions.CreateEnvironmentConfiguration())
            .Build();
    }

    public static string? ApiToken => Instance["MARKETDATA_TOKEN"];

    public static bool Enabled =>
        bool.TryParse(Instance["MARKETDATA_RUN_INTEGRATION_TESTS"], out var enabled)
        && enabled;
}

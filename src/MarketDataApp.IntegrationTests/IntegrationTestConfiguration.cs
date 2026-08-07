using Microsoft.Extensions.Configuration;

namespace MarketDataApp.IntegrationTests;

internal static class IntegrationTestConfiguration
{
    public static IConfiguration Instance { get; } = BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        var config = MarketDataClientOptions.CreateEnvironmentConfiguration();
        config["MARKETDATA_MAX_RETRIES"] = "1";
        return config;
    }

    public static string? ApiToken => Instance["MARKETDATA_TOKEN"];

    public static bool Enabled =>
        bool.TryParse(Instance["MARKETDATA_RUN_INTEGRATION_TESTS"], out var enabled)
        && enabled;
}

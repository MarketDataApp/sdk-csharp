namespace MarketDataApp.IntegrationTests;

/// <summary>
/// Live coverage for the two startup-validation paths. These are the only integration tests that
/// leave <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> enabled; the shared
/// <see cref="IntegrationTestBase"/> opts out so the rest of the suite does not spend a
/// <c>GET /user/</c> per test.
/// </summary>
public sealed class StartupValidationIntegrationTests
{
    [IntegrationFact]
    public void Constructor_WithLiveToken_ValidatesAndSeedsRateLimit()
    {
        using var httpClient = new HttpClient();

        // The blocking GET /user/ must succeed (no exception) and seed the snapshot before
        // any endpoint call is made.
        using var client = new MarketDataClient(
            httpClient,
            MarketDataClientOptions.FromConfiguration(IntegrationTestConfiguration.Instance));

        Assert.NotNull(client.LatestRateLimit);
    }

    [IntegrationFact]
    public async Task CreateAsync_WithLiveToken_ValidatesAndSeedsRateLimit()
    {
        using var httpClient = new HttpClient();

        using var client = await MarketDataClient.CreateAsync(
            httpClient,
            MarketDataClientOptions.FromConfiguration(IntegrationTestConfiguration.Instance));

        Assert.NotNull(client.LatestRateLimit);
    }
}

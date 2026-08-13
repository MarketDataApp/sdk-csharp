namespace MarketDataApp.IntegrationTests;

public abstract class IntegrationTestBase : IDisposable
{
    private readonly HttpClient _httpClient = new();

    protected IntegrationTestBase()
    {
        // Shared endpoint fixtures skip startup validation: xUnit constructs the test class per
        // test, and a live GET /user/ per instance would burn quota and slow the suite. Both
        // startup-validation paths are exercised deliberately in StartupValidationIntegrationTests.
        Client = new MarketDataClient(
            _httpClient,
            MarketDataClientOptions.FromConfiguration(IntegrationTestConfiguration.Instance) with
            {
                ValidateTokenOnStartup = false
            });
    }

    protected MarketDataClient Client { get; }

    protected static void AssertSuccess(int statusCode) =>
        Assert.Contains(statusCode, new[] { 200, 203 });

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

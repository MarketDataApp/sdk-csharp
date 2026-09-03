namespace MarketDataApp.IntegrationTests;

/// <summary>
/// A live test. Skipped only when the suite is not enabled at all
/// (<c>MARKETDATA_RUN_INTEGRATION_TESTS</c> unset), so a plain <c>dotnet test</c> on a
/// fresh clone stays green. Once the suite is enabled, a missing token is <b>not</b> a
/// reason to skip: SDK requirements section 13 says the suite must fail in that case,
/// and <see cref="IntegrationTestConfiguration.RequireApiToken"/> makes every test do so
/// with an explanatory message.
/// </summary>
internal sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!IntegrationTestConfiguration.Enabled)
        {
            Skip = "Configure MARKETDATA_RUN_INTEGRATION_TESTS=true to run live integration tests.";
        }
    }
}

namespace MarketDataApp.IntegrationTests;

public sealed class UtilitiesIntegrationTests : IntegrationTestBase
{
    [IntegrationFact]
    public async Task Status_ReturnsExpectedShapes()
    {
        var status = await Client.Utilities.GetStatusAsync();

        AssertSuccess(status.StatusCode);
        Assert.NotEmpty(status.Values);
        Assert.All(status.Values, service => Assert.False(string.IsNullOrWhiteSpace(service.Service)));
    }

    [IntegrationFact]
    public async Task User_ReturnsExpectedShapes()
    {
        var user = await Client.Utilities.GetUserAsync();

        AssertSuccess(user.StatusCode);
        Assert.True(user.Values.RequestsLimit >= 0);
    }

    [IntegrationFact]
    public async Task Headers_ReturnExpectedShapes()
    {
        var headers = await Client.Utilities.GetHeadersAsync();

        AssertSuccess(headers.StatusCode);
        Assert.NotEmpty(headers.Values);
    }
}

using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Markets;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Markets;

public sealed class MarketsApiCoverageTests
{
    [Fact]
    public async Task GetStatusAsync_MapsClosedStatusAndIsClosed()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "date": [1706745600],
          "status": ["closed"]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var status = (await client.Markets.GetStatusAsync(new MarketStatusRequest())).Values[0];

        Assert.False(status.IsOpen);
        Assert.True(status.IsClosed);
    }

    [Fact]
    public void MarketStatus_IsOpenIsClosed_HandleNullStatus()
    {
        var unknown = new MarketStatus(new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero), null);
        Assert.False(unknown.IsOpen);
        Assert.False(unknown.IsClosed);
        Assert.Equal("2025-01-10 n/a", unknown.ToString());
    }

    [Fact]
    public async Task GetStatusAsync_RejectsInvalidCountryCode()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request expected."));
        var client = MarketDataTestClient.Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Markets.GetStatusAsync(new MarketStatusRequest { Country = "USA" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Markets.GetStatusCsvAsync(new MarketStatusRequest { Country = "USA" }));
    }

    [Fact]
    public async Task GetStatusCsvAsync_ScalarOverload_HitsExpectedPath()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("date,status\r\n2025-01-10,open\r\n", Encoding.UTF8, "text/csv")
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Markets.GetStatusCsvAsync(
            country: "US", date: new DateOnly(2025, 1, 10));

        Assert.Equal("date,status\r\n2025-01-10,open\r\n", response.Csv);
        Assert.Equal("/v1/markets/status/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("country=US", handler.LastRequest.RequestUri.Query);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
    }
}

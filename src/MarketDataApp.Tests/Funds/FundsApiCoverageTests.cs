using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Funds;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Funds;

public sealed class FundsApiCoverageTests
{
    [Fact]
    public async Task GetCandlesAsync_FullPayload_MapsEveryField()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "t": [1706745600],
          "o": [450.10],
          "h": [452.00],
          "l": [449.50],
          "c": [451.25]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var candle = (await client.Funds.GetCandlesAsync(
            new FundCandlesRequest(FundResolution.Daily, "VTI"))).Values[0];

        Assert.NotNull(candle.Time);
        Assert.Equal(450.10m, candle.Open);
        Assert.Equal(452.00m, candle.High);
        Assert.Equal(449.50m, candle.Low);
        Assert.Equal(451.25m, candle.Close);
    }

    [Fact]
    public async Task GetCandlesCsvAsync_ScalarOverload_HitsExpectedPath()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("t,c\r\n1,451.25\r\n", Encoding.UTF8, "text/csv")
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Funds.GetCandlesCsvAsync(
            FundResolution.Months(3), "VXUS", date: new DateOnly(2024, 2, 1));

        Assert.Equal("t,c\r\n1,451.25\r\n", response.Csv);
        Assert.Equal("/v1/funds/candles/3M/VXUS/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
        Assert.Contains("date=2024-02-01", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public void FundCandle_ToString_RendersConciseSummary()
    {
        Assert.Equal(
            "2024-01-02 O=450.1 H=452 L=449.5 C=451.25",
            new FundCandle(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 450.1m, 452m, 449.5m, 451.25m).ToString());
    }
}

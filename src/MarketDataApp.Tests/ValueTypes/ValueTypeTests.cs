using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Funds;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.ValueTypes;

/// <summary>
/// Coverage-completing tests for the resolution value types and the wire-format enum mappings:
/// factory validation, <c>WireValue</c>/<c>ToString</c>, and the ArgumentOutOfRange guard for
/// out-of-range enum values sent through the query builder.
/// </summary>
public sealed class ValueTypeTests
{
    [Fact]
    public void StockResolution_FactoriesProduceExpectedWireValues()
    {
        Assert.Equal("D", StockResolution.Daily.WireValue);
        Assert.Equal("W", StockResolution.Weekly.WireValue);
        Assert.Equal("M", StockResolution.Monthly.WireValue);
        Assert.Equal("Y", StockResolution.Yearly.WireValue);
        Assert.Equal("5", StockResolution.Minutes(5).WireValue);
        Assert.Equal("4H", StockResolution.Hours(4).WireValue);
        Assert.Equal("3D", StockResolution.Days(3).WireValue);
        Assert.Equal("2W", StockResolution.Weeks(2).WireValue);
        Assert.Equal("3M", StockResolution.Months(3).WireValue);
        Assert.Equal("2Y", StockResolution.Years(2).WireValue);
        Assert.Equal("custom", StockResolution.Of("custom").WireValue);
        Assert.Equal("4H", StockResolution.Hours(4).ToString());
    }

    [Fact]
    public void StockResolution_FactoriesRejectNonPositiveAndBlank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Minutes(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Hours(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Days(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Weeks(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Months(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockResolution.Years(0));
        Assert.Throws<ArgumentException>(() => StockResolution.Of("  "));
    }

    [Fact]
    public void StockResolution_DefaultInstance_WireValueThrows()
    {
        Assert.Throws<InvalidOperationException>(() => default(StockResolution).WireValue);
    }

    [Fact]
    public void FundResolution_FactoriesProduceExpectedWireValues()
    {
        Assert.Equal("D", FundResolution.Daily.WireValue);
        Assert.Equal("W", FundResolution.Weekly.WireValue);
        Assert.Equal("M", FundResolution.Monthly.WireValue);
        Assert.Equal("Y", FundResolution.Yearly.WireValue);
        Assert.Equal("5D", FundResolution.Days(5).WireValue);
        Assert.Equal("2W", FundResolution.Weeks(2).WireValue);
        Assert.Equal("3M", FundResolution.Months(3).WireValue);
        Assert.Equal("2Y", FundResolution.Years(2).WireValue);
        Assert.Equal("custom", FundResolution.Of("custom").WireValue);
        Assert.Equal("3M", FundResolution.Months(3).ToString());
    }

    [Fact]
    public void FundResolution_FactoriesRejectNonPositiveAndBlank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FundResolution.Days(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FundResolution.Weeks(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FundResolution.Months(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FundResolution.Years(0));
        Assert.Throws<ArgumentException>(() => FundResolution.Of(""));
    }

    [Fact]
    public void FundResolution_DefaultInstance_WireValueThrows()
    {
        Assert.Throws<InvalidOperationException>(() => default(FundResolution).WireValue);
    }

    [Fact]
    public async Task DateFormatSpreadsheet_IsSerializedOnTheWire()
    {
        // Spreadsheet is a CSV-only format: typed methods reject it (see StocksApiTests), so the
        // wire serialization contract is exercised through a CSV endpoint.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("symbol,mid\r\nAAPL,1.0\r\n", Encoding.UTF8, "text/csv")
        });
        var client = MarketDataTestClient.Create(handler);

        await client.Stocks.GetPricesCsvAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions { DateFormat = DateFormat.Spreadsheet });

        Assert.Contains("dateformat=spreadsheet", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task OutOfRangeEnumValues_ThrowWhenBuildingTheQuery()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request expected."));
        var client = MarketDataTestClient.Create(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions { DateFormat = (DateFormat)999 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions { Mode = (Mode)999 }));
    }
}

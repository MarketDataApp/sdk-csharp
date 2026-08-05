using MarketDataApp.Stocks;

namespace MarketDataApp.IntegrationTests;

public sealed class StocksIntegrationTests : IntegrationTestBase
{
    [IntegrationFact]
    public async Task Quote_ReturnsExpectedShape()
    {
        var response = await Client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL"));

        AssertSuccess(response.StatusCode);
        Assert.Contains(response.Values, value => value.Symbol == "AAPL");
    }

    [IntegrationFact]
    public async Task Candles_ReturnExpectedShape()
    {
        var response = await Client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Daily, "AAPL")
            {
                To = DateOnly.FromDateTime(DateTime.UtcNow),
                Countback = 5
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
    }

    [IntegrationFact]
    public async Task PriceCsv_ReturnsExpectedShape()
    {
        var response = await Client.Stocks.GetPriceCsvAsync(new StockPriceRequest("AAPL"));

        AssertSuccess(response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Csv));
    }

    [IntegrationFact]
    public async Task Prices_MultiSymbol_ReturnExpectedShape()
    {
        var response = await Client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL", "MSFT"));

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.Contains(response.Values, value => value.Symbol == "AAPL");
    }

    [IntegrationFact]
    public async Task Earnings_ReturnExpectedShape()
    {
        var response = await Client.Stocks.GetEarningsAsync(
            new StockEarningsRequest("AAPL") { Countback = 4 });

        // Earnings availability depends on the account's data entitlement; the SDK must
        // round-trip correctly either way: data on success, or the documented no-data
        // response (HTTP 404 -> IsNoData) rather than throwing.
        if (response.IsNoData)
        {
            Assert.Equal(404, response.StatusCode);
        }
        else
        {
            AssertSuccess(response.StatusCode);
            Assert.NotEmpty(response.Values);
        }
    }

    [IntegrationFact]
    public async Task News_ReturnExpectedShape()
    {
        var response = await Client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL") { Countback = 5 });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
    }

    [IntegrationFact]
    public async Task Quotes_MultiSymbol_ReturnExpectedShape()
    {
        var response = await Client.Stocks.GetQuotesAsync(
            new StockQuotesRequest("AAPL", "MSFT"));

        AssertSuccess(response.StatusCode);
        Assert.Contains(response.Values, v => v.Symbol == "AAPL");
    }
}

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
    public async Task Candles_LongIntradayRange_HasNoDuplicateOrMissingBoundaryBars()
    {
        // Spans exactly one year-chunk boundary (>1 year of 30-minute bars). Guards both failure
        // modes of chunking against the live API: duplicated boundary-day bars (overlapping
        // chunks under the inclusive from/to contract) and missing boundary days (half-open
        // chunks against a server that excluded the endpoint).
        var response = await Client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Minutes(30), "AAPL")
            {
                From = new DateOnly(2024, 3, 5),
                To = new DateOnly(2025, 4, 4)
            });

        AssertSuccess(response.StatusCode);
        Assert.True(response.IsComposite);
        Assert.NotEmpty(response.Values);
        Assert.Equal(response.Values.Count, response.Values.DistinctBy(candle => candle.Time).Count());
        // Chunk boundary for this range: 2025-03-05 / 2025-03-06, both regular trading days.
        // Bars must exist on each side of the boundary.
        Assert.Contains(response.Values, candle =>
            candle.Time is { } time && DateOnly.FromDateTime(time.Date) == new DateOnly(2025, 3, 5));
        Assert.Contains(response.Values, candle =>
            candle.Time is { } time && DateOnly.FromDateTime(time.Date) == new DateOnly(2025, 3, 6));
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

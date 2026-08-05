using MarketDataApp.Funds;
using MarketDataApp.Markets;
using MarketDataApp.Options;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Validation;

public sealed class RequestValidationTests
{
    private static readonly StubHttpMessageHandler NoRequestHandler =
        new(_ => throw new InvalidOperationException("No HTTP request was expected."));

    [Fact]
    public async Task DateWindow_AllowsCountbackWithTo()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""{"s":"no_data"}"""));
        var client = MarketDataTestClient.Create(handler);

        await client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL")
            {
                To = new DateOnly(2025, 1, 10),
                Countback = 5
            });

        Assert.Contains("to=2025-01-10", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("countback=5", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task DateWindow_RejectsCountbackWithFromAcrossResources()
    {
        var client = MarketDataTestClient.Create(NoRequestHandler);
        var from = new DateOnly(2025, 1, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Funds.GetCandlesAsync(
            new FundCandlesRequest(FundResolution.Daily, "VTI") { From = from, Countback = 5 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Markets.GetStatusAsync(
            new MarketStatusRequest { From = from, Countback = 5 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL") { From = from, Countback = 5 }));
    }

    [Fact]
    public async Task InvalidPagination_IsRejectedBeforeSending()
    {
        var client = MarketDataTestClient.Create(NoRequestHandler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL"),
            new MarketDataRequestOptions { Limit = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Stocks.GetQuoteCsvAsync(
            new StockQuoteRequest("AAPL"),
            new MarketDataRequestOptions { Offset = -1 }));
    }

    [Fact]
    public async Task EarningsReport_RejectsDateWindow()
    {
        var client = MarketDataTestClient.Create(NoRequestHandler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetEarningsAsync(
            new StockEarningsRequest("AAPL")
            {
                Report = "2025-Q1",
                Date = new DateOnly(2025, 3, 31)
            }));
    }

    [Fact]
    public async Task Chain_RejectsInvalidNumericFilters()
    {
        var client = MarketDataTestClient.Create(NoRequestHandler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL") { Delta = 1.1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL") { MinBid = 10, MaxBid = 1 }));
    }
}

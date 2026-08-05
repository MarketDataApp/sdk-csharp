using MarketDataApp;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Validation;

public sealed class RequestValidationCoverageTests
{
    private static readonly StubHttpMessageHandler NoRequest =
        new(_ => throw new InvalidOperationException("No HTTP request was expected."));

    [Fact]
    public async Task DateWindow_RejectsNonPositiveCountback()
    {
        var client = MarketDataTestClient.Create(NoRequest);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL") { Countback = 0 }));
    }

    [Fact]
    public async Task DateWindow_RejectsDateCombinedWithFrom()
    {
        var client = MarketDataTestClient.Create(NoRequest);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL")
            {
                Date = new DateOnly(2025, 1, 1),
                From = new DateOnly(2025, 1, 2)
            }));
    }

    [Fact]
    public async Task DateWindow_RejectsDateCombinedWithTo()
    {
        var client = MarketDataTestClient.Create(NoRequest);

        // From is unset here, so the guard reaches (and is tripped by) the To condition.
        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL")
            {
                Date = new DateOnly(2025, 1, 1),
                To = new DateOnly(2025, 1, 2)
            }));
    }

    [Fact]
    public async Task EmptyColumns_AreOmittedFromQuery()
    {
        var handler = new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}"""));
        var client = MarketDataTestClient.Create(handler);

        // An empty (but non-null) Columns list is valid and simply emits no "columns" parameter.
        await client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL"),
            new MarketDataRequestOptions { Columns = Array.Empty<string>() });

        Assert.DoesNotContain("columns=", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task DateWindow_RejectsFromAfterTo()
    {
        var client = MarketDataTestClient.Create(NoRequest);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL")
            {
                From = new DateOnly(2025, 2, 1),
                To = new DateOnly(2025, 1, 1)
            }));
    }

    [Fact]
    public async Task RequestOptions_RejectBlankColumns()
    {
        var client = MarketDataTestClient.Create(NoRequest);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL"),
            new MarketDataRequestOptions { Columns = ["symbol", "  "] }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SymbolRequests_RejectEmptyBlankAndNull(int _)
    {
        Assert.Throws<ArgumentException>(() => new StockPricesRequest(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new StockPricesRequest(" "));
        Assert.Throws<ArgumentNullException>(() => new StockPricesRequest((IEnumerable<string>)null!));

        Assert.Throws<ArgumentException>(() => new StockQuotesRequest(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new StockQuotesRequest(" "));
        Assert.Throws<ArgumentNullException>(() => new StockQuotesRequest((IEnumerable<string>)null!));
    }

    [Fact]
    public void WithLogger_SetsLoggerAndValidatesArguments()
    {
        var logger = new CapturingLogger();
        var options = new MarketDataClientOptions().WithLogger(logger);

        Assert.Same(logger, options.Logger);
        Assert.Throws<ArgumentNullException>(() => ((MarketDataClientOptions)null!).WithLogger(logger));
        Assert.Throws<ArgumentNullException>(() => new MarketDataClientOptions().WithLogger(null!));
    }
}

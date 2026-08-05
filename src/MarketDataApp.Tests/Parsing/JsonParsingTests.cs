using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Options;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Parsing;

/// <summary>
/// Exercises <c>JsonResponseParser</c> edge cases through the public typed endpoints: ISO-8601 and
/// unparseable timestamps, null array elements, non-array parallel fields, and non-string status.
/// </summary>
public sealed class JsonParsingTests
{
    private static MarketDataClient Client(string body) =>
        MarketDataTestClient.Create(new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(body)));

    [Fact]
    public async Task Timestamp_ParsesIso8601StringIntoEasternInstant()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "mid": [190.25],
          "updated": ["2024-01-31T12:00:00Z"]
        }
        """);

        var price = (await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"))).Values[0];

        Assert.NotNull(price.Updated);
        Assert.Equal(
            new DateTimeOffset(2024, 1, 31, 12, 0, 0, TimeSpan.Zero),
            price.Updated!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task Timestamp_UnparseableStringDecodesToNull()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "mid": [190.25],
          "updated": ["not-a-timestamp"]
        }
        """);

        var price = (await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"))).Values[0];

        Assert.Null(price.Updated);
    }

    [Fact]
    public async Task NullArrayElement_DecodesToNullField()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "ask": [null],
          "mid": [190.25]
        }
        """);

        var quote = (await client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL"))).Values[0];

        Assert.Null(quote.Ask);
        Assert.Equal(190.25m, quote.Mid);
    }

    [Fact]
    public async Task NonArrayParallelField_MapsToParseException()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "last": "190.25"
        }
        """);

        var exception = await Assert.ThrowsAsync<ParseException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        Assert.Contains("must be an array", exception.InnerException!.Message);
    }

    [Fact]
    public async Task NonStringStatus_MapsToParseExceptionDescribingKind()
    {
        var client = Client("""
        {
          "s": 5,
          "symbol": ["AAPL"],
          "last": [190.25]
        }
        """);

        var exception = await Assert.ThrowsAsync<ParseException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
        Assert.Contains("Number", exception.InnerException!.Message);
    }

    [Fact]
    public async Task NonBooleanField_DecodesToNull()
    {
        var client = Client("""
        {
          "s": "ok",
          "optionSymbol": ["AAPL250117C00150000"],
          "inTheMoney": [5]
        }
        """);

        var quote = (await client.Options.GetChainAsync(new OptionsChainRequest("AAPL"))).Values[0];

        Assert.Null(quote.InTheMoney);
    }
}

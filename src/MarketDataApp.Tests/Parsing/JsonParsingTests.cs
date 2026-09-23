using System.Globalization;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Funds;
using MarketDataApp.Options;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Parsing;

/// <summary>
/// Exercises <c>JsonResponseParser</c> edge cases through the public typed endpoints: ISO-8601,
/// date-only and unparseable timestamps, null array elements, non-array parallel fields, and
/// non-string status.
/// </summary>
public sealed class JsonParsingTests
{
    private static readonly MarketDataRequestOptions TimestampFormat = new() { DateFormat = DateFormat.Timestamp };

    private static MarketDataClient Client(string body) =>
        MarketDataTestClient.Create(new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(body)));

    /// <summary>Renders a timestamp as its wall-clock time and offset, so a test pins both.</summary>
    /// <param name="value">The decoded timestamp.</param>
    /// <returns>The timestamp as <c>yyyy-MM-ddTHH:mm:sszzz</c>, or <see langword="null"/> when absent.</returns>
    private static string? WithOffset(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

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

    /// <summary>Verifies that a daily stock candle's date reads as midnight US/Eastern of that day.</summary>
    [Fact]
    public async Task Timestamp_DailyStockCandleDateIsUsEasternMidnight()
    {
        var client = Client("""{"s":"ok","t":["2026-09-21"],"c":[190.25]}""");

        var candle = (await client.Stocks.GetCandlesAsync(
            StockResolution.Daily, "AAPL", countback: 1, options: TimestampFormat)).Values[0];

        Assert.Equal("2026-09-21T00:00:00-04:00", WithOffset(candle.Time));
    }

    /// <summary>Verifies that a daily fund candle's date reads as midnight US/Eastern of that day.</summary>
    [Fact]
    public async Task Timestamp_DailyFundCandleDateIsUsEasternMidnight()
    {
        var client = Client("""{"s":"ok","t":["2025-03-03"],"c":[451.25]}""");

        var candle = (await client.Funds.GetCandlesAsync(
            FundResolution.Daily, "VFINX", countback: 1, options: TimestampFormat)).Values[0];

        Assert.Equal("2025-03-03T00:00:00-05:00", WithOffset(candle.Time));
    }

    /// <summary>Verifies that an earning's date, report date and update date read as midnight US/Eastern.</summary>
    [Fact]
    public async Task Timestamp_EarningDatesAreUsEasternMidnight()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "date": ["2025-12-31"],
          "reportDate": ["2026-01-29"],
          "updated": ["2026-09-21"]
        }
        """);

        var earning = (await client.Stocks.GetEarningsAsync("AAPL", countback: 1, options: TimestampFormat)).Values[0];

        Assert.Equal("2025-12-31T00:00:00-05:00", WithOffset(earning.Date));
        Assert.Equal("2026-01-29T00:00:00-05:00", WithOffset(earning.ReportDate));
        Assert.Equal("2026-09-21T00:00:00-04:00", WithOffset(earning.Updated));
    }

    /// <summary>Verifies that a news article's publication date and the response's update date read as midnight US/Eastern.</summary>
    [Fact]
    public async Task Timestamp_NewsDatesAreUsEasternMidnight()
    {
        var client = Client("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "headline": ["Headline"],
          "content": ["Content"],
          "source": ["https://example.com/story"],
          "publicationDate": ["2026-09-18"],
          "updated": "2026-09-21"
        }
        """);

        var response = await client.Stocks.GetNewsAsync("AAPL", countback: 1, options: TimestampFormat);

        Assert.Equal("2026-09-18T00:00:00-04:00", WithOffset(response.Values[0].PublicationDate));
        Assert.Equal("2026-09-21T00:00:00-04:00", WithOffset(response.Updated));
    }

    /// <summary>Verifies that option expirations read as midnight US/Eastern while a datetime keeps its offset.</summary>
    [Fact]
    public async Task Timestamp_OptionExpirationDatesAreUsEasternMidnight()
    {
        var client = Client("""
        {
          "s": "ok",
          "expirations": ["2026-09-25", "2027-01-15"],
          "updated": "2026-09-21 14:46:05 -04:00"
        }
        """);

        var response = await client.Options.GetExpirationsAsync("AAPL", options: TimestampFormat);

        Assert.Equal(
            new string?[] { "2026-09-25T00:00:00-04:00", "2027-01-15T00:00:00-05:00" },
            response.Values.Select(expiration => WithOffset(expiration)));
        Assert.Equal("2026-09-21T14:46:05-04:00", WithOffset(response.Updated));
    }

    /// <summary>Verifies that option expirations read as midnight US/Eastern on the default format, where the API sends them as dates.</summary>
    [Fact]
    public async Task DefaultFormat_OptionExpirationDatesAreUsEasternMidnight()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "expirations": ["2026-09-25"],
          "updated": 1790102263
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetExpirationsAsync("AAPL");

        Assert.DoesNotContain("dateformat", handler.LastRequest!.RequestUri!.Query);
        Assert.Equal("2026-09-25T00:00:00-04:00", WithOffset(response.Values[0]));
        Assert.Equal("2026-09-22T14:37:43-04:00", WithOffset(response.Updated));
    }

    /// <summary>Verifies that a market status date reads as midnight US/Eastern of that day.</summary>
    [Fact]
    public async Task Timestamp_MarketStatusDateIsUsEasternMidnight()
    {
        var client = Client("""{"s":"ok","date":["2026-09-21"],"status":["open"]}""");

        var status = (await client.Markets.GetStatusAsync(countback: 1, options: TimestampFormat)).Values[0];

        Assert.Equal("2026-09-21T00:00:00-04:00", WithOffset(status.Date));
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

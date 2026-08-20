using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Options;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Options;

/// <summary>
/// Coverage-completing tests for <see cref="OptionsApi"/> and the options value types:
/// full-payload <see cref="OptionQuote"/> mapping, greek helpers, expiration/strike filters,
/// strike-map parse errors, wire-value mapping, and the scalar / CSV convenience overloads.
/// </summary>
public sealed class OptionsApiCoverageTests
{
    private static StubHttpMessageHandler Json(string body) =>
        new(_ => MarketDataTestClient.JsonResponse(body));

    private static StubHttpMessageHandler Csv(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
        });

    private const string FullChainRow = """
    {
      "s": "ok",
      "optionSymbol": ["AAPL250117C00150000"],
      "underlying": ["AAPL"],
      "expiration": [1737072000],
      "side": ["call"],
      "strike": [150],
      "firstTraded": [1700000000],
      "dte": [10],
      "updated": [1736985600],
      "bid": [5.20],
      "bidSize": [12],
      "mid": [5.25],
      "ask": [5.30],
      "askSize": [8],
      "last": [5.22],
      "openInterest": [1000],
      "volume": [250],
      "inTheMoney": [true],
      "intrinsicValue": [2.5],
      "extrinsicValue": [2.75],
      "underlyingPrice": [152.5],
      "iv": [0.35],
      "delta": [0.55],
      "gamma": [0.02],
      "theta": [-0.03],
      "vega": [0.10],
      "rho": [0.05]
    }
    """;

    [Fact]
    public async Task GetChainAsync_FullPayload_MapsEveryOptionQuoteField()
    {
        var client = MarketDataTestClient.Create(Json(FullChainRow));

        var quote = (await client.Options.GetChainAsync(new OptionsChainRequest("AAPL"))).Values[0];

        Assert.Equal("AAPL250117C00150000", quote.OptionSymbol);
        Assert.Equal("AAPL", quote.Underlying);
        Assert.NotNull(quote.Expiration);
        Assert.Equal("call", quote.Side);
        Assert.Equal(150m, quote.Strike);
        Assert.NotNull(quote.FirstTraded);
        Assert.Equal(10, quote.Dte);
        Assert.NotNull(quote.Updated);
        Assert.Equal(5.20m, quote.Bid);
        Assert.Equal(12, quote.BidSize);
        Assert.Equal(5.25m, quote.Mid);
        Assert.Equal(5.30m, quote.Ask);
        Assert.Equal(8, quote.AskSize);
        Assert.Equal(5.22m, quote.Last);
        Assert.Equal(1000, quote.OpenInterest);
        Assert.Equal(250, quote.Volume);
        Assert.True(quote.InTheMoney);
        Assert.Equal(2.5m, quote.IntrinsicValue);
        Assert.Equal(2.75m, quote.ExtrinsicValue);
        Assert.Equal(152.5m, quote.UnderlyingPrice);
        Assert.Equal(0.35, quote.Iv);
        Assert.Equal(0.55, quote.Delta);
        Assert.Equal(0.02, quote.Gamma);
        Assert.Equal(-0.03, quote.Theta);
        Assert.Equal(0.10, quote.Vega);
        Assert.Equal(0.05, quote.Rho);
    }

    [Fact]
    public async Task OptionQuote_GreekHelpers_ReportPresentGreeksAndLookup()
    {
        var client = MarketDataTestClient.Create(Json(FullChainRow));
        var quote = (await client.Options.GetChainAsync(new OptionsChainRequest("AAPL"))).Values[0];

        Assert.Equal(
            new[] { Greek.Delta, Greek.Gamma, Greek.Theta, Greek.Vega, Greek.Rho },
            quote.PresentGreeks.OrderBy(g => g));
        Assert.Equal(0.55, quote.GetGreek(Greek.Delta));
        Assert.Equal(0.02, quote.GetGreek(Greek.Gamma));
        Assert.Equal(-0.03, quote.GetGreek(Greek.Theta));
        Assert.Equal(0.10, quote.GetGreek(Greek.Vega));
        Assert.Equal(0.05, quote.GetGreek(Greek.Rho));
        Assert.Throws<ArgumentOutOfRangeException>(() => quote.GetGreek((Greek)999));

        Assert.Equal("AAPL250117C00150000 mid=5.25 last=5.22", quote.ToString());
    }

    [Fact]
    public void OptionQuote_WithNoGreeks_HasEmptyPresentGreeks()
    {
        var quote = new OptionQuote(
            "AAPL250117C00150000", "AAPL", null, "call", 150m, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        Assert.Empty(quote.PresentGreeks);
        Assert.Null(quote.GetGreek(Greek.Delta));
    }

    [Fact]
    public async Task GetChainAsync_MapsExpirationFilterVariantsAndStrikeFilters()
    {
        // OnDate expiration filter + Exact strike filter.
        var handler = Json(FullChainRow);
        var client = MarketDataTestClient.Create(handler);

        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Expiration = ExpirationFilter.ForDate(new DateOnly(2025, 1, 17)),
            Strike = StrikeFilter.ForExact(150m)
        });
        Assert.Contains("expiration=2025-01-17", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("strike=150", handler.LastRequest.RequestUri.Query);

        // Dte expiration filter + Comparison strike filters (all operators).
        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Expiration = ExpirationFilter.ForDte(30),
            Strike = StrikeFilter.ForComparison(StrikeFilter.ComparisonOperator.Gt, 100m)
        });
        Assert.Contains("dte=30", handler.LastRequest.RequestUri.Query);
        Assert.Contains("strike=%3e100", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);

        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Strike = StrikeFilter.ForComparison(StrikeFilter.ComparisonOperator.Gte, 100m)
        });
        Assert.Contains("strike=%3e%3d100", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);

        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Strike = StrikeFilter.ForComparison(StrikeFilter.ComparisonOperator.Lt, 100m)
        });
        Assert.Contains("strike=%3c100", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);

        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Strike = StrikeFilter.ForComparison(StrikeFilter.ComparisonOperator.Lte, 100m)
        });
        Assert.Contains("strike=%3c%3d100", handler.LastRequest.RequestUri.Query, StringComparison.OrdinalIgnoreCase);

        // MonthYear expiration filter.
        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Expiration = ExpirationFilter.ForMonthYear(2025, 6)
        });
        Assert.Contains("month=6", handler.LastRequest.RequestUri.Query);
        Assert.Contains("year=2025", handler.LastRequest.RequestUri.Query);

        // All expiration filter + StrikeRange variants + OptionSide.Put.
        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            Expiration = ExpirationFilter.AllDates(),
            StrikeRangeFilter = StrikeRange.Otm,
            Side = OptionSide.Put
        });
        Assert.Contains("expiration=all", handler.LastRequest.RequestUri.Query);
        Assert.Contains("range=otm", handler.LastRequest.RequestUri.Query);
        Assert.Contains("side=put", handler.LastRequest.RequestUri.Query);

        await client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
        {
            StrikeRangeFilter = StrikeRange.All
        });
        Assert.Contains("range=all", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetChainAsync_InvalidEnumValues_ThrowArgumentOutOfRange()
    {
        var client = MarketDataTestClient.Create(Json(FullChainRow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { Side = (OptionSide)999 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { StrikeRangeFilter = (StrikeRange)999 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL")
            {
                Strike = StrikeFilter.ForComparison((StrikeFilter.ComparisonOperator)999, 100m)
            }));
    }

    [Fact]
    public void ExpirationAndStrikeFilterFactories_ValidateArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationFilter.ForDte(-1));
        Assert.Throws<ArgumentException>(() =>
            ExpirationFilter.ForRange(new DateOnly(2025, 3, 1), new DateOnly(2025, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationFilter.ForMonthYear(2025, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExpirationFilter.ForMonthYear(2025, 0));
        Assert.Throws<ArgumentException>(() => StrikeFilter.ForRange(200m, 100m));

        // Valid factories expose their data.
        Assert.Equal(new DateOnly(2025, 1, 17), ((ExpirationFilter.OnDate)ExpirationFilter.ForDate(new DateOnly(2025, 1, 17))).Date);
        Assert.Equal(30, ((ExpirationFilter.Dte)ExpirationFilter.ForDte(30)).Days);
        Assert.Equal(2025, ((ExpirationFilter.MonthYear)ExpirationFilter.ForMonthYear(2025, 6)).Year);
        Assert.Equal(150m, ((StrikeFilter.Exact)StrikeFilter.ForExact(150m)).Price);
        Assert.Equal(
            StrikeFilter.ComparisonOperator.Lte,
            ((StrikeFilter.Comparison)StrikeFilter.ForComparison(StrikeFilter.ComparisonOperator.Lte, 5m)).Op);
    }

    [Fact]
    public async Task GetChainAsync_RejectsAdditionalInvalidFilters()
    {
        var client = MarketDataTestClient.Create(Json(FullChainRow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { StrikeLimit = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { MinAsk = -1m }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { MinAsk = 10m, MaxAsk = 1m }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { MaxBidAskSpread = -1m }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { MinOpenInterest = -1 }));
        // Delta above +1 and below -1 are both out of range.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { Delta = 1.5 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { Delta = -1.5 }));
        // The percentage spread filter cannot be negative.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Options.GetChainAsync(new OptionsChainRequest("AAPL") { MaxBidAskSpreadPct = -0.5 }));
    }

    [Fact]
    public async Task ScalarAndCsvOverloads_HitExpectedPaths()
    {
        var handler = Csv("col\r\n1\r\n");
        var client = MarketDataTestClient.Create(handler);

        // JSON scalar overloads.
        var jsonHandler = Json("""{"s":"ok","optionSymbol":"AAPL250117C00150000"}""");
        var jsonClient = MarketDataTestClient.Create(jsonHandler);
        await jsonClient.Options.GetLookupAsync("AAPL 150 Call");
        Assert.StartsWith("/v1/options/lookup/", jsonHandler.LastRequest!.RequestUri!.AbsolutePath);

        var expClient = MarketDataTestClient.Create(Json("""{"s":"ok","expirations":[1737072000]}"""));
        await expClient.Options.GetExpirationsAsync("AAPL", strike: 150m);

        var chainClient = MarketDataTestClient.Create(Json(FullChainRow));
        await chainClient.Options.GetChainAsync("AAPL", weekly: true);

        var quotesClient = MarketDataTestClient.Create(Json("""{"s":"ok","optionSymbol":["AAPL250117C00150000"]}"""));
        var quotes = await quotesClient.Options.GetQuotesAsync(["AAPL250117C00150000"], date: new DateOnly(2025, 1, 10));
        Assert.Single(quotes);

        // CSV scalar overloads.
        await client.Options.GetLookupCsvAsync("AAPL 150 Call");
        Assert.StartsWith("/v1/options/lookup/", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.Options.GetExpirationsCsvAsync("AAPL", strike: 150m, date: new DateOnly(2025, 1, 10));
        Assert.Equal("/v1/options/expirations/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("strike=150", handler.LastRequest.RequestUri.Query);

        await client.Options.GetQuoteCsvAsync("AAPL250117C00150000", to: new DateOnly(2025, 1, 10));
        Assert.Equal("/v1/options/quotes/AAPL250117C00150000/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("to=2025-01-10", handler.LastRequest.RequestUri.Query);

        var csvQuotes = await client.Options.GetQuotesCsvAsync(
            ["AAPL250117C00150000", "MSFT250117C00400000"], from: new DateOnly(2025, 1, 1));
        Assert.Equal(2, csvQuotes.Count);

        await client.Options.GetChainCsvAsync("AAPL", side: OptionSide.Call);
        Assert.Equal("/v1/options/chain/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("side=call", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetExpirationsAsync_NullExpirationValue_MapsToParseException()
    {
        var client = MarketDataTestClient.Create(Json("""{"s":"ok","expirations":[null]}"""));

        var exception = await Assert.ThrowsAsync<ParseException>(() =>
            client.Options.GetExpirationsAsync(new OptionsExpirationsRequest("AAPL")));
        Assert.Contains("Missing expirations", exception.InnerException!.Message);
    }

    [Fact]
    public async Task GetLookupAsync_MissingOrNonStringSymbol_MapsToParseException()
    {
        var missing = MarketDataTestClient.Create(Json("""{"s":"ok"}"""));
        var e1 = await Assert.ThrowsAsync<ParseException>(() =>
            missing.Options.GetLookupAsync(new OptionsLookupRequest("AAPL 150 Call")));
        Assert.Contains("Missing optionSymbol", e1.InnerException!.Message);

        var nonString = MarketDataTestClient.Create(Json("""{"s":"ok","optionSymbol":123}"""));
        var e2 = await Assert.ThrowsAsync<ParseException>(() =>
            nonString.Options.GetLookupAsync(new OptionsLookupRequest("AAPL 150 Call")));
        Assert.Contains("Missing optionSymbol", e2.InnerException!.Message);
    }

    [Fact]
    public async Task GetChainCsvAsync_SerializesRangeFilterWithoutSide()
    {
        var handler = Csv("optionSymbol\r\nAAPL250117C00150000\r\n");
        var client = MarketDataTestClient.Create(handler);

        await client.Options.GetChainCsvAsync(new OptionsChainRequest("AAPL")
        {
            StrikeRangeFilter = StrikeRange.Itm,
            StrikeLimit = 3,
            Delta = 0.4
        });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("range=itm", query);
        Assert.Contains("strikeLimit=3", query);
        Assert.DoesNotContain("side=", query);
    }

    [Fact]
    public void OptionsQuotesRequest_RejectsEmptyAndBlankAndNullSymbols()
    {
        Assert.Throws<ArgumentException>(() => new OptionsQuotesRequest(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new OptionsQuotesRequest(" "));
        Assert.Throws<ArgumentNullException>(() => new OptionsQuotesRequest((IEnumerable<string>)null!));
    }
}

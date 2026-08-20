using MarketDataApp;
using MarketDataApp.Options;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Options;

public sealed class OptionsApiTests
{
    [Fact]
    public async Task GetLookupAsync_MapsInputAndParsesSymbol()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "optionSymbol": "AAPL250117C00150000"
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetLookupAsync(new OptionsLookupRequest("AAPL 150 Call"));

        Assert.Equal("AAPL250117C00150000", response.Values);
        Assert.Equal("/v1/options/lookup/AAPL%20150%20Call/", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetExpirationsAsync_MapsFiltersAndUpdated()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "expirations": [1737072000],
          "updated": 1736985600
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetExpirationsAsync(
            new OptionsExpirationsRequest("AAPL")
            {
                Strike = 150,
                Date = new DateOnly(2025, 1, 10)
            });

        Assert.Single(response.Values);
        Assert.NotNull(response.Updated);
        Assert.Equal("/v1/options/expirations/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("strike=150", handler.LastRequest.RequestUri.Query);
        Assert.Contains("date=2025-01-10", handler.LastRequest.RequestUri.Query);
        Assert.DoesNotContain("nonstandard", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetQuoteAsync_MapsWindowAndParsesQuote()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "optionSymbol": ["AAPL250117C00150000"],
          "underlying": ["AAPL"],
          "expiration": [1737072000],
          "side": ["call"],
          "strike": [150],
          "last": [12.5],
          "dte": [10],
          "updated": [1736985600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetQuoteAsync(
            new OptionsQuoteRequest("AAPL250117C00150000")
            {
                To = new DateOnly(2025, 1, 10)
            });

        Assert.Equal(12.5m, response.Values[0].Last);
        Assert.Equal(10, response.Values[0].Dte);
        Assert.Equal("/v1/options/quotes/AAPL250117C00150000/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("to=2025-01-10", handler.LastRequest.RequestUri.Query);
        Assert.DoesNotContain("countback", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetQuotesAsync_FetchesEachSymbol()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref requestCount);
            return MarketDataTestClient.JsonResponse("""
            {
              "s": "ok",
              "optionSymbol": ["AAPL250117C00150000"],
              "last": [12.5]
            }
            """);
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetQuotesAsync(
            new OptionsQuotesRequest("AAPL250117C00150000", "MSFT250117C00400000"));

        Assert.Equal(2, requestCount);
        Assert.Equal(2, response.Count);
        Assert.All(response.Values, value => Assert.Equal(12.5m, value.Values[0].Last));
    }

    [Fact]
    public async Task GetChainAsync_MapsAllChainFilters()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "optionSymbol": ["AAPL250117C00150000"],
          "underlying": ["AAPL"],
          "expiration": [1737072000],
          "side": ["call"],
          "strike": [150],
          "last": [12.5]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForRange(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1)),
                Weekly = true,
                Monthly = false,
                Quarterly = true,
                Am = true,
                Pm = false,
                NonStandard = true,
                Strike = StrikeFilter.ForRange(140, 160),
                Delta = 0.5,
                StrikeLimit = 5,
                StrikeRangeFilter = StrikeRange.Itm,
                MinBid = 1.25m,
                MaxBid = 10m,
                MinAsk = 1.5m,
                MaxAsk = 11m,
                MaxBidAskSpread = 0.5m,
                MaxBidAskSpreadPct = 0.1,
                MinOpenInterest = 100,
                MinVolume = 20,
                Side = OptionSide.Call,
                Date = new DateOnly(2025, 1, 10)
            });

        Assert.Equal(12.5m, response.Values[0].Last);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Equal("/v1/options/chain/AAPL/", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Contains("from=2025-01-01", query);
        Assert.Contains("to=2025-03-01", query);
        Assert.Contains("weekly=true", query);
        Assert.Contains("monthly=false", query);
        Assert.Contains("quarterly=true", query);
        Assert.Contains("am=true", query);
        Assert.Contains("pm=false", query);
        Assert.Contains("nonstandard=true", query);
        Assert.Contains("strike=140-160", query);
        Assert.Contains("delta=0.5", query);
        Assert.Contains("strikeLimit=5", query);
        Assert.Contains("range=itm", query);
        Assert.Contains("minBid=1.25", query);
        Assert.Contains("maxBid=10", query);
        Assert.Contains("minAsk=1.5", query);
        Assert.Contains("maxAsk=11", query);
        Assert.Contains("maxBidAskSpread=0.5", query);
        Assert.Contains("maxBidAskSpreadPct=0.1", query);
        Assert.Contains("minOpenInterest=100", query);
        Assert.Contains("minVolume=20", query);
        Assert.Contains("side=call", query);
        Assert.Contains("date=2025-01-10", query);
    }

    [Fact]
    public async Task GetQuoteAsync_ScalarOverloadBuildsRequest()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""{"s":"ok","optionSymbol":["AAPL250117C00150000"]}"""));
        var client = MarketDataTestClient.Create(handler);

        await client.Options.GetQuoteAsync("AAPL250117C00150000", date: new DateOnly(2025, 1, 10));

        Assert.Contains("date=2025-01-10", handler.LastRequest!.RequestUri!.Query);
    }

}

using MarketDataApp;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Stocks;

public sealed class StocksApiTests
{
    [Fact]
    public async Task GetQuoteAsync_SendsBearerTokenAndEndpointParameters()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "last": [190.25],
          "updated": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler, "secret-token");

        var response = await client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL") { Candle = true },
            new MarketDataRequestOptions { Mode = Mode.Live });

        Assert.Equal("AAPL", response.Values[0].Symbol);
        Assert.Equal(190.25m, response.Values[0].Last);
        Assert.Equal("Bearer secret-token", handler.LastRequest!.Headers.Authorization!.ToString());
        Assert.Equal("/v1/stocks/quotes/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("candle=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("mode=live", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task TypedMethods_RejectSpreadsheetDateFormat()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new InvalidOperationException("typed spreadsheet requests must fail before any HTTP call"));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL"),
            new MarketDataRequestOptions { DateFormat = DateFormat.Spreadsheet }));

        Assert.Contains("Spreadsheet", exception.Message);
        Assert.Contains("CsvAsync", exception.Message);
    }

    [Fact]
    public async Task TypedMethods_RejectSpreadsheetClientDefaultDateFormat()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new InvalidOperationException("typed spreadsheet requests must fail before any HTTP call"));
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultDateFormat = DateFormat.Spreadsheet
        });

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
    }

    [Fact]
    public async Task GetPricesAsync_ParsesPriceRows()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL", "MSFT"],
          "mid": [190.25, 420.5],
          "change": [1.25, -2.5],
          "changepct": [0.01, -0.005],
          "updated": [1706745600, 1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL", "MSFT"));

        Assert.Equal(2, response.Values.Count);
        Assert.Equal("MSFT", response.Values[1].Symbol);
        Assert.Contains("symbols=AAPL%2CMSFT", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetQuotesAsync_ParsesBatchQuotes()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "mid": [190.25],
          "updated": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetQuotesAsync(
            new StockQuotesRequest("AAPL") { Week52 = true });

        Assert.Equal("AAPL", response.Values[0].Symbol);
        Assert.Contains("/v1/stocks/quotes/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("52week=true", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetCandlesAsync_MapsPathAndCandleParameters()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "t": [1706745600],
          "o": [189.0],
          "h": [191.0],
          "l": [188.5],
          "c": [190.25],
          "v": [1000000]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Daily, "AAPL")
            {
                From = new DateOnly(2024, 2, 1),
                To = new DateOnly(2024, 2, 2),
                AdjustSplits = true
            });

        Assert.Equal(190.25m, response.Values[0].Close);
        Assert.Equal("/v1/stocks/candles/D/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("adjustsplits=true", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetCandlesAsync_ChunksLongIntradayRangesAndMergesResults()
    {
        var requests = new List<Uri>();
        var handler = new StubHttpMessageHandler(request =>
        {
            lock (requests)
            {
                requests.Add(request.RequestUri!);
            }

            return MarketDataTestClient.JsonResponse("""
            {
              "s": "ok",
              "t": [1706745600],
              "o": [189.0],
              "h": [191.0],
              "l": [188.5],
              "c": [190.25],
              "v": [1000000]
            }
            """);
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            });

        Assert.Equal(3, requests.Count);
        Assert.Equal(3, response.Values.Count);
        Assert.True(response.IsComposite);
        Assert.Equal(3, response.Parts.Count);
        Assert.Null(response.RequestId);
        Assert.Null(response.RateLimit);
        Assert.Equal(string.Empty, response.RawBody);
        Assert.Contains(requests, uri => uri.Query.Contains("from=2020-01-01"));
        Assert.Contains(requests, uri => uri.Query.Contains("from=2020-12-31"));
        Assert.Contains(requests, uri => uri.Query.Contains("from=2021-12-31"));
    }

    [Fact]
    public async Task GetCandlesAsync_MapsNoDataToEmptyValues()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Daily, "UNKNOWN"));

        Assert.True(response.IsNoData);
        Assert.Empty(response.Values);
    }

    [Fact]
    public async Task GetCandlesAsync_AllNoDataChunksPreserveCompositeNoDataState()
    {
        var handler = new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse("""{"s":"no_data"}"""));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "UNKNOWN")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            });

        Assert.True(response.IsComposite);
        Assert.True(response.IsNoData);
        Assert.Equal(3, response.Parts.Count);
        Assert.Empty(response.Values);
    }

    [Fact]
    public async Task GetCandlesAsync_RejectsConflictingWindowParameters()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("No request expected."));
        var client = MarketDataTestClient.Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Daily, "AAPL")
            {
                Date = new DateOnly(2025, 1, 1),
                Countback = 5
            }));
    }

    [Fact]
    public async Task TypedEndpoint_RejectsInvalidResponseStatus()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "error",
          "symbol": ["AAPL"],
          "last": [190.25]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        await Assert.ThrowsAsync<MarketDataApp.Exceptions.ParseException>(
            () => client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL")));
    }

    [Fact]
    public async Task GetNewsAsync_ParsesArticlesAndScalarUpdated()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "headline": ["Apple reports results"],
          "content": ["Summary"],
          "source": ["https://example.com/article"],
          "publicationDate": [1706745600],
          "updated": 1706749200
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetNewsAsync(new StockNewsRequest("AAPL"));

        Assert.Equal("Apple reports results", response.Values[0].Headline);
        Assert.NotNull(response.Updated);
    }

    [Fact]
    public async Task GetEarningsAsync_ParsesEarningsRows()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "fiscalYear": [2024],
          "fiscalQuarter": [4],
          "date": [1727654400],
          "reportDate": [1730332800],
          "reportTime": ["amc"],
          "currency": ["USD"],
          "reportedEPS": [1.64],
          "estimatedEPS": [1.6],
          "surpriseEPS": [0.04],
          "surpriseEPSpct": [0.025],
          "updated": [1730332800]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetEarningsAsync(
            new StockEarningsRequest("AAPL") { Report = "2024-Q4" });

        Assert.Equal(2024, response.Values[0].FiscalYear);
        Assert.Equal(1.64m, response.Values[0].ReportedEps);
        Assert.Contains("report=2024-Q4", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetPriceAsync_UsesSingleSymbolPath()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "mid": [190.25],
          "updated": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetPriceAsync(new StockPriceRequest("AAPL"));

        Assert.Equal(190.25m, response.Values[0].Mid);
        Assert.Equal("/v1/stocks/prices/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetQuoteAsync_ScalarOverloadBuildsRequest()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""{"s":"ok","symbol":["AAPL"],"last":[190.25]}"""));
        var client = MarketDataTestClient.Create(handler);

        await client.Stocks.GetQuoteAsync("AAPL", candle: true);

        Assert.Contains("candle=true", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetNewsAsync_SendsColumnsAndParsesProjectedSubset()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "headline": ["Apple reports results"],
          "content": ["Summary"],
          "source": ["https://example.com/article"],
          "publicationDate": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetNewsAsync(
            new StockNewsRequest("AAPL"),
            new MarketDataRequestOptions
            {
                Columns = ["symbol", "headline", "content", "source", "publicationDate"]
            });

        Assert.Equal("Apple reports results", response.Values[0].Headline);
        Assert.Null(response.Updated);
        Assert.Contains(
            "columns=symbol%2Cheadline%2Ccontent%2Csource%2CpublicationDate",
            handler.LastRequest!.RequestUri!.Query);
    }

}

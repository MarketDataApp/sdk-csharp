using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Stocks;

/// <summary>
/// Coverage-completing tests for <see cref="StocksApi"/>: full-payload field mapping (every wire
/// field decoded into the correct model property), the scalar convenience overloads, the CSV
/// overloads, and the record <c>ToString()</c> summaries.
/// </summary>
public sealed class StocksApiCoverageTests
{
    private static StubHttpMessageHandler Json(string body) =>
        new(_ => MarketDataTestClient.JsonResponse(body));

    private static StubHttpMessageHandler Csv(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
        });

    [Fact]
    public async Task GetQuoteAsync_FullPayload_MapsEveryField()
    {
        var handler = Json("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "ask": [150.30],
          "askSize": [100],
          "bid": [150.20],
          "bidSize": [200],
          "mid": [150.25],
          "last": [150.10],
          "change": [1.25],
          "changepct": [0.0084],
          "volume": [123456],
          "updated": [1706745600],
          "o": [149.0],
          "h": [151.0],
          "l": [148.5],
          "c": [150.10],
          "52weekHigh": [199.0],
          "52weekLow": [120.0]
        }
        """);
        var client = MarketDataTestClient.Create(handler);

        var quote = (await client.Stocks.GetQuoteAsync(
            new StockQuoteRequest("AAPL") { Candle = true, Week52 = true, Extended = true }))
            .Values[0];

        Assert.Equal("AAPL", quote.Symbol);
        Assert.Equal(150.30m, quote.Ask);
        Assert.Equal(100, quote.AskSize);
        Assert.Equal(150.20m, quote.Bid);
        Assert.Equal(200, quote.BidSize);
        Assert.Equal(150.25m, quote.Mid);
        Assert.Equal(150.10m, quote.Last);
        Assert.Equal(1.25m, quote.Change);
        Assert.Equal(0.0084, quote.ChangePct);
        Assert.Equal(123456, quote.Volume);
        Assert.NotNull(quote.Updated);
        Assert.Equal(149.0m, quote.Open);
        Assert.Equal(151.0m, quote.High);
        Assert.Equal(148.5m, quote.Low);
        Assert.Equal(150.10m, quote.Close);
        Assert.Equal(199.0m, quote.Week52High);
        Assert.Equal(120.0m, quote.Week52Low);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("extended=true", query);
        Assert.Contains("52week=true", query);
    }

    [Fact]
    public async Task GetPricesAsync_FullPayload_MapsChangeAndChangePct()
    {
        var handler = Json("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "mid": [190.25],
          "change": [1.25],
          "changepct": [0.0066],
          "updated": [1706745600]
        }
        """);
        var client = MarketDataTestClient.Create(handler);

        var price = (await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"))).Values[0];

        Assert.Equal(190.25m, price.Mid);
        Assert.Equal(1.25m, price.Change);
        Assert.Equal(0.0066, price.ChangePct);
        Assert.NotNull(price.Updated);
    }

    [Fact]
    public async Task GetCandlesAsync_FullPayload_MapsEveryField()
    {
        var handler = Json("""
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
        var client = MarketDataTestClient.Create(handler);

        var candle = (await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Daily, "AAPL"))).Values[0];

        Assert.NotNull(candle.Time);
        Assert.Equal(189.0m, candle.Open);
        Assert.Equal(191.0m, candle.High);
        Assert.Equal(188.5m, candle.Low);
        Assert.Equal(190.25m, candle.Close);
        Assert.Equal(1000000, candle.Volume);
    }

    [Fact]
    public async Task GetEarningsAsync_FullPayload_MapsEveryField()
    {
        var handler = Json("""
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
          "estimatedEPS": [1.60],
          "surpriseEPS": [0.04],
          "surpriseEPSpct": [0.025],
          "updated": [1730332800]
        }
        """);
        var client = MarketDataTestClient.Create(handler);

        var earning = (await client.Stocks.GetEarningsAsync(new StockEarningsRequest("AAPL"))).Values[0];

        Assert.Equal("AAPL", earning.Symbol);
        Assert.Equal(2024, earning.FiscalYear);
        Assert.Equal(4, earning.FiscalQuarter);
        Assert.NotNull(earning.Date);
        Assert.NotNull(earning.ReportDate);
        Assert.Equal("amc", earning.ReportTime);
        Assert.Equal("USD", earning.Currency);
        Assert.Equal(1.64m, earning.ReportedEps);
        Assert.Equal(1.60m, earning.EstimatedEps);
        Assert.Equal(0.04m, earning.SurpriseEps);
        Assert.Equal(0.025, earning.SurpriseEpsPct);
        Assert.NotNull(earning.Updated);
    }

    [Fact]
    public async Task GetNewsAsync_FullPayload_MapsEveryField()
    {
        var handler = Json("""
        {
          "s": "ok",
          "symbol": ["AAPL"],
          "headline": ["Apple unveils results"],
          "content": ["Full body"],
          "source": ["https://example.com/a"],
          "publicationDate": [1706745600]
        }
        """);
        var client = MarketDataTestClient.Create(handler);

        var article = (await client.Stocks.GetNewsAsync(new StockNewsRequest("AAPL"))).Values[0];

        Assert.Equal("AAPL", article.Symbol);
        Assert.Equal("Apple unveils results", article.Headline);
        Assert.Equal("Full body", article.Content);
        Assert.Equal("https://example.com/a", article.Source);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1706745600),
            article.PublicationDate.ToUniversalTime());
    }

    // ---- Scalar convenience overloads ------------------------------------

    [Fact]
    public async Task ScalarOverloads_BuildRequestsAndHitExpectedPaths()
    {
        var handler = Json("""{"s":"ok","symbol":["AAPL"],"mid":[1.0],"t":[1],"o":[1],"h":[1],"l":[1],"c":[1],"v":[1],"headline":["x"],"content":["y"],"source":["z"],"publicationDate":[1],"fiscalYear":[2024]}""");
        var client = MarketDataTestClient.Create(handler);

        await client.Stocks.GetPricesAsync(["AAPL", "MSFT"]);
        Assert.Equal("/v1/stocks/prices/", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.Stocks.GetPriceAsync("AAPL");
        Assert.Equal("/v1/stocks/prices/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetQuotesAsync(["AAPL"], candle: true);
        Assert.Equal("/v1/stocks/quotes/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("candle=true", handler.LastRequest.RequestUri.Query);

        await client.Stocks.GetBulkQuotesAsync(["AAPL"], snapshot: true);
        Assert.Equal("/v1/stocks/bulkquotes/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetCandlesAsync(StockResolution.Daily, "AAPL", countback: 5);
        Assert.Equal("/v1/stocks/candles/D/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("countback=5", handler.LastRequest.RequestUri.Query);

        await client.Stocks.GetNewsAsync("AAPL", countback: 3);
        Assert.Equal("/v1/stocks/news/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetEarningsAsync("AAPL", report: "2024-Q4");
        Assert.Equal("/v1/stocks/earnings/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("report=2024-Q4", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task CsvScalarOverloads_HitExpectedPathsWithCsvFormat()
    {
        var handler = Csv("col\r\n1\r\n");
        var client = MarketDataTestClient.Create(handler);

        await client.Stocks.GetQuoteCsvAsync("AAPL", candle: true);
        Assert.Equal("/v1/stocks/quotes/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
        Assert.Contains("candle=true", handler.LastRequest.RequestUri.Query);

        await client.Stocks.GetPricesCsvAsync(["AAPL", "MSFT"]);
        Assert.Equal("/v1/stocks/prices/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetPriceCsvAsync("AAPL");
        Assert.Equal("/v1/stocks/prices/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetQuotesCsvAsync(["AAPL"], week52: true);
        Assert.Equal("/v1/stocks/quotes/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("52week=true", handler.LastRequest.RequestUri.Query);

        await client.Stocks.GetBulkQuotesCsvAsync(["AAPL"], extended: true);
        Assert.Equal("/v1/stocks/bulkquotes/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetCandlesCsvAsync(StockResolution.Daily, "AAPL", date: new DateOnly(2025, 1, 2));
        Assert.Equal("/v1/stocks/candles/D/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("date=2025-01-02", handler.LastRequest.RequestUri.Query);

        await client.Stocks.GetNewsCsvAsync("AAPL", countback: 2);
        Assert.Equal("/v1/stocks/news/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);

        await client.Stocks.GetEarningsCsvAsync("AAPL", report: "2024-Q4");
        Assert.Equal("/v1/stocks/earnings/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("report=2024-Q4", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task CandlesCsvSingleChunk_ReturnsBodyForNonIntradayResolution()
    {
        var handler = Csv("t,c\r\n1706745600,190.25\r\n");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesCsvAsync(
            new StockCandlesRequest(StockResolution.Daily, "AAPL")
            {
                From = new DateOnly(2024, 1, 1),
                To = new DateOnly(2024, 2, 1)
            });

        Assert.False(response.IsComposite);
        Assert.Equal("t,c\r\n1706745600,190.25\r\n", response.Csv);
    }

    // ---- Record ToString summaries (populated + null variants) -----------

    [Fact]
    public void RecordToStrings_RenderConciseSummaries_AndHandleNulls()
    {
        Assert.Equal(
            "AAPL mid=150.25 last=150.10",
            new StockQuote("AAPL", null, null, null, null, 150.25m, 150.10m, null, null, null, null,
                null, null, null, null, null, null).ToString());
        Assert.Equal(
            "n/a mid=n/a last=n/a",
            new StockQuote(null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null).ToString());

        Assert.Equal(
            "AAPL mid=190.25 chg=1.25",
            new StockPrice("AAPL", 190.25m, 1.25m, null, null).ToString());

        Assert.Equal(
            "2024-01-02 O=189 H=191 L=188.5 C=190.25 V=1000000",
            new StockCandle(new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero), 189m, 191m, 188.5m, 190.25m, 1000000).ToString());
        Assert.Equal(
            "n/a O=n/a H=n/a L=n/a C=n/a V=n/a",
            new StockCandle(null, null, null, null, null, null).ToString());

        Assert.Equal(
            "AAPL FY2024 Q1 eps=1.5",
            new StockEarning("AAPL", 2024, 1, null, null, null, null, 1.5m, null, null, null, null).ToString());

        // Short headline (no truncation) and long headline (truncated with ellipsis).
        var shortArticle = new StockNewsArticle(
            "AAPL", "Short headline", "c", "s", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("AAPL: Short headline (2024-01-02)", shortArticle.ToString());

        var longHeadline = new string('x', 80);
        var longArticle = new StockNewsArticle(
            "AAPL", longHeadline, "c", "s", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var text = longArticle.ToString();
        Assert.Contains("…", text);
        Assert.DoesNotContain(longHeadline, text);
    }

    [Theory]
    [InlineData("symbol", "Missing symbol")]
    [InlineData("headline", "Missing headline")]
    [InlineData("content", "Missing content")]
    [InlineData("source", "Missing source")]
    [InlineData("publicationDate", "Missing publicationDate")]
    public async Task GetNewsAsync_NullRequiredField_ThrowsParseException(string field, string expected)
    {
        var fields = new Dictionary<string, string>
        {
            ["symbol"] = """["AAPL"]""",
            ["headline"] = """["h"]""",
            ["content"] = """["c"]""",
            ["source"] = """["s"]""",
            ["publicationDate"] = "[1706745600]"
        };
        fields[field] = "[null]";
        var body = "{\"s\":\"ok\"," + string.Join(",", fields.Select(kv => $"\"{kv.Key}\":{kv.Value}")) + "}";
        var client = MarketDataTestClient.Create(Json(body));

        var exception = await Assert.ThrowsAsync<ParseException>(
            () => client.Stocks.GetNewsAsync(new StockNewsRequest("AAPL")));
        Assert.Contains(expected, exception.InnerException!.Message);
    }

    [Fact]
    public async Task OkResponseWithNoDataArrays_YieldsEmptyValues()
    {
        // An "ok" status with no field arrays present exercises the empty-length branch of the
        // parallel-array reader (distinct from the 404/no_data short-circuits).
        var handler = Json("""{"s":"ok"}""");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL"));

        Assert.False(response.IsNoData);
        Assert.Empty(response.Values);
    }

    [Fact]
    public async Task GetCandlesAsync_IntradayFromOnOrAfterTo_SendsSingleRequest()
    {
        var requests = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            return MarketDataTestClient.JsonResponse("""{"s":"ok","t":[1],"c":[1.0]}""");
        });
        var client = MarketDataTestClient.Create(handler);

        // Intraday resolution with an unbounded (null To) window whose From is today: the computed
        // upper bound (now) is <= From, so chunking collapses to a single request.
        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
            });

        Assert.Equal(1, requests);
        Assert.False(response.IsComposite);
    }

    [Fact]
    public async Task GetCandlesAsync_PartialContentChunk_PropagatesStatus203()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var body = MarketDataTestClient.JsonResponse("""{"s":"ok","t":[1],"c":[1.0]}""");
            // Return 203 for one of the chunked requests; the composite status must surface it.
            if (request.RequestUri!.Query.Contains("from=2020-01-01", StringComparison.Ordinal))
            {
                return new HttpResponseMessage((HttpStatusCode)203) { Content = body.Content };
            }

            return body;
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            });

        Assert.True(response.IsComposite);
        Assert.Equal(203, response.StatusCode);
    }

    [Fact]
    public async Task GetCandlesCsvAsync_MergesChunksWithoutTrailingNewline_AndHonorsHeadersFalse()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            // First chunk has NO trailing newline; later chunks do. With headers=false the reader
            // must still stitch rows and insert a separator before a chunk that follows one lacking
            // an end-of-line.
            var content = request.RequestUri!.Query.Contains("from=2020-01-01", StringComparison.Ordinal)
                ? "1,101"
                : "2,102\r\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv")
            };
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesCsvAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            },
            new MarketDataRequestOptions { Headers = false });

        Assert.True(response.IsComposite);
        Assert.Contains("1,101", response.Csv);
        Assert.Contains("2,102", response.Csv);
        // The first chunk lacked a trailing newline, so a separator was inserted before the next row.
        Assert.DoesNotContain("1,1012,102", response.Csv);
    }

    [Fact]
    public async Task GetCandlesCsvAsync_StripsHeaderOnlyChunkWithNoTrailingNewline()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            // The first chunk carries the header plus a data row; later chunks are header-only with
            // no trailing newline, exercising the header-strip path where the newline is absent.
            var content = request.RequestUri!.Query.Contains("from=2020-01-01", StringComparison.Ordinal)
                ? "t,c\r\n1,101\r\n"
                : "t,c";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/csv")
            };
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesCsvAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            });

        // Only the first chunk's header survives; the header-only chunks collapse to nothing.
        Assert.Equal(1, response.Csv.Split("t,c", StringSplitOptions.None).Length - 1);
        Assert.Contains("1,101", response.Csv);
    }
}

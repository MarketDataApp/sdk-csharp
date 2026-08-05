using System.Net;
using System.Text;
using MarketDataApp.Markets;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Responses;

/// <summary>
/// Tests for §11.6 response-object features: format detection (IsJson/IsCsv/IsHtml),
/// concise ToString summaries, and extension-aware SaveToFile.
/// </summary>
public sealed class ResponseFeaturesTests
{
    private const string QuoteJson = """
    {
      "s": "ok",
      "symbol": ["AAPL"],
      "mid": [150.25],
      "last": [150.10],
      "updated": [1706745600]
    }
    """;

    private const string PricesCsv = "symbol,mid\r\nAAPL,150.25\r\n";

    // ---- Format detection -------------------------------------------------

    [Fact]
    public async Task TypedJsonResponse_ReportsJsonFormat()
    {
        var client = MarketDataTestClient.Create(
            new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(QuoteJson)));

        var response = await client.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));

        Assert.True(response.IsJson);
        Assert.False(response.IsCsv);
        Assert.False(response.IsHtml);
        Assert.IsAssignableFrom<IMarketDataResponse>(response);
    }

    [Fact]
    public async Task CsvResponse_ReportsCsvFormat()
    {
        var client = MarketDataTestClient.Create(CsvHandler(PricesCsv));

        var response = await client.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));

        Assert.False(response.IsJson);
        Assert.True(response.IsCsv);
        Assert.False(response.IsHtml);
        Assert.IsAssignableFrom<IMarketDataResponse>(response);
    }

    [Fact]
    public void HtmlResponse_ReportsHtmlFormat()
    {
        var response = new HtmlResponse();

        Assert.False(response.IsJson);
        Assert.False(response.IsCsv);
        Assert.True(response.IsHtml);
        Assert.IsAssignableFrom<IMarketDataResponse>(response);
    }

    // ---- ToString summaries ----------------------------------------------

    [Fact]
    public async Task TypedResponse_ToString_IsConciseSummary()
    {
        var client = MarketDataTestClient.Create(
            new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(QuoteJson)));

        var response = await client.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));

        Assert.Equal("StockQuotesResponse: 1 item, HTTP 200", response.ToString());
        // Not the default record dump / raw payload.
        Assert.DoesNotContain("Values =", response.ToString());
        Assert.DoesNotContain("{", response.ToString());
    }

    [Fact]
    public async Task NoDataResponse_ToString_IncludesMarker()
    {
        var client = MarketDataTestClient.Create(
            new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""{ "s": "no_data" }""")));

        var response = await client.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));

        Assert.True(response.IsNoData);
        Assert.Equal("StockQuotesResponse: 0 items, HTTP 200, no data", response.ToString());
    }

    [Fact]
    public async Task CsvResponse_ToString_ReportsByteLength()
    {
        var client = MarketDataTestClient.Create(CsvHandler(PricesCsv));

        var response = await client.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));

        // "symbol,mid\r\nAAPL,150.25\r\n" is 25 ASCII bytes.
        Assert.Equal("CsvResponse: 25 bytes, HTTP 200", response.ToString());
    }

    [Fact]
    public void HtmlResponse_ToString_ReportsByteLength()
    {
        var response = new HtmlResponse();

        var text = response.ToString();

        Assert.StartsWith("HtmlResponse:", text, StringComparison.Ordinal);
        Assert.Contains("bytes", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StockQuote_ToString_IsConciseOneLine()
    {
        var client = MarketDataTestClient.Create(
            new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(QuoteJson)));

        var response = await client.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));

        Assert.Equal("AAPL mid=150.25 last=150.10", response.Values[0].ToString());
    }

    [Fact]
    public void MarketStatus_ToString_IsConciseOneLine()
    {
        var status = new MarketStatus(new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero), "open");

        var text = status.ToString();

        Assert.Equal("2025-01-10 open", text);
        // Not the default record dump.
        Assert.DoesNotContain("Status =", text);
    }

    // ---- SaveToFile honors the extension ---------------------------------

    [Fact]
    public async Task SaveToFile_WritesCsvToMatchingExtension_AndReturnsPath()
    {
        var client = MarketDataTestClient.Create(CsvHandler(PricesCsv));
        var response = await client.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));
        var path = Path.Combine(Path.GetTempPath(), $"mdapp-{Guid.NewGuid():N}.csv");

        try
        {
            var returned = response.SaveToFile(path);

            Assert.Equal(path, returned);
            Assert.Equal(response.Csv, File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveToFileAsync_WritesJsonBodyToMatchingExtension()
    {
        var client = MarketDataTestClient.Create(
            new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse(QuoteJson)));
        var response = await client.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));
        var path = Path.Combine(Path.GetTempPath(), $"mdapp-{Guid.NewGuid():N}.json");

        try
        {
            var returned = await response.SaveToFileAsync(path);

            Assert.Equal(path, returned);
            Assert.Equal(response.RawBody, File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveToFile_FallsBackToRawBodyForUnknownExtension()
    {
        var client = MarketDataTestClient.Create(CsvHandler(PricesCsv));
        var response = await client.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));
        var path = Path.Combine(Path.GetTempPath(), $"mdapp-{Guid.NewGuid():N}.dat");

        try
        {
            response.SaveToFile(path);

            Assert.Equal(response.RawBody, File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static StubHttpMessageHandler CsvHandler(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
        });
}

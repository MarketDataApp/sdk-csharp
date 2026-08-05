using System.Net;
using System.Text;
using MarketDataApp;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Responses;

/// <summary>
/// Coverage-completing tests for <see cref="MarketDataResponse{T}"/> features: extension-aware
/// <c>SaveToFile</c> for the HTML facet and format-mismatch fallbacks, the item-count summary for
/// null and single-value payloads, and <see cref="HtmlResponse.Html"/>.
/// </summary>
public sealed class ResponseCoverageTests
{
    private static StubHttpMessageHandler Json(string body) =>
        new(_ => MarketDataTestClient.JsonResponse(body));

    private static StubHttpMessageHandler Csv(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
        });

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"mdapp-{Guid.NewGuid():N}{extension}");

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void HtmlResponse_SaveToFile_WritesHtmlRepresentation(string extension)
    {
        var response = new HtmlResponse();
        var path = TempPath(extension);
        try
        {
            var returned = response.SaveToFile(path);

            Assert.Equal(path, returned);
            Assert.True(File.Exists(path));
            Assert.Equal(response.RawBody, File.ReadAllText(path));
            Assert.Equal(response.Values, response.Html);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveToFile_MismatchedExtension_FallsBackToRawBody()
    {
        var jsonClient = MarketDataTestClient.Create(Json("""{"s":"ok","symbol":["AAPL"],"mid":[1.0]}"""));
        var jsonResponse = await jsonClient.Stocks.GetQuotesAsync(new StockQuotesRequest("AAPL"));
        // A JSON response written to a .csv path cannot provide CSV, so it falls back to the raw body.
        var csvPath = TempPath(".csv");

        var csvClient = MarketDataTestClient.Create(Csv("symbol,mid\r\nAAPL,1.0\r\n"));
        var csvResponse = await csvClient.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));
        // A CSV response written to a .json path cannot provide JSON, so it falls back to the raw body.
        var jsonPath = TempPath(".json");

        try
        {
            jsonResponse.SaveToFile(csvPath);
            csvResponse.SaveToFile(jsonPath);

            Assert.Equal(jsonResponse.RawBody, File.ReadAllText(csvPath));
            Assert.Equal(csvResponse.RawBody, File.ReadAllText(jsonPath));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
        }
    }

    [Fact]
    public void EmptyResponse_ToString_ReportsZeroItemsForNullValues()
    {
        // A default-constructed response has null Values, exercising the null arm of the item count.
        var response = new StockQuotesResponse();

        Assert.Equal("StockQuotesResponse: 0 items, HTTP 0", response.ToString());
    }

    [Fact]
    public async Task SingleValueResponse_ToString_ReportsOneItem()
    {
        var client = MarketDataTestClient.Create(Json("""
        {
          "x-ratelimit-requests-remaining": 99000,
          "x-ratelimit-requests-limit": 100000,
          "x-options-data-permissions": ""
        }
        """));

        var response = await client.Utilities.GetUserAsync();

        // User is neither null nor a collection, so it counts as a single item.
        Assert.Equal("UtilitiesUserResponse: 1 item, HTTP 200", response.ToString());
    }
}

using System.Net;
using System.Text;
using MarketDataApp.Funds;
using MarketDataApp.Markets;
using MarketDataApp.Options;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Csv;

public sealed class CsvApiTests
{
    [Fact]
    public async Task StocksCsv_PreservesBodyMetadataAndCsvOptions()
    {
        var handler = CreateHandler("symbol,mid\r\nAAPL,150.25\r\n");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetPricesCsvAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions
            {
                Headers = false,
                Human = true,
                Columns = ["symbol", "mid"]
            });

        Assert.Equal("symbol,mid\r\nAAPL,150.25\r\n", response.Values);
        Assert.Equal(response.Values, response.RawBody);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("request-1", response.RequestId);
        Assert.NotNull(response.RateLimit);
        Assert.Equal("/v1/stocks/prices/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
        Assert.Contains("headers=false", handler.LastRequest.RequestUri.Query);
        Assert.Contains("human=true", handler.LastRequest.RequestUri.Query);
        Assert.Contains("columns=symbol%2Cmid", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task FundsCsv_UsesFundEndpoint()
    {
        var handler = CreateHandler("t,o,h,l,c\r\n1737072000,1,2,0.5,1.5\r\n");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Funds.GetCandlesCsvAsync(
            new FundCandlesRequest(FundResolution.Daily, "SPY"));

        Assert.Contains("1737072000", response.Csv);
        Assert.Equal("/v1/funds/candles/D/SPY/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task OptionsCsv_MapsChainFilters()
    {
        var handler = CreateHandler("optionSymbol,strike\r\nAAPL250117C00150000,150\r\n");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Options.GetChainCsvAsync(
            new OptionsChainRequest("AAPL") { Side = OptionSide.Call });

        Assert.Contains("optionSymbol", response.Values);
        Assert.Equal("/v1/options/chain/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
        Assert.Contains("side=call", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task MarketsCsv_UsesStatusEndpoint()
    {
        var handler = CreateHandler("date,status\r\n2025-01-10,open\r\n");
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Markets.GetStatusCsvAsync(
            new MarketStatusRequest { Country = "US" });

        Assert.Equal("date,status\r\n2025-01-10,open\r\n", response.Values);
        Assert.Equal("/v1/markets/status/", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("country=US", handler.LastRequest.RequestUri.Query);
        Assert.Contains("format=csv", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task LongRangeStockCandlesCsv_ChunksAndDeduplicatesHeaders()
    {
        var requests = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            var row = Interlocked.Increment(ref requests);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"t,c\r\n{row},{100 + row}\r\n", Encoding.UTF8, "text/csv")
            };
        });
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Stocks.GetCandlesCsvAsync(
            new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
            {
                From = new DateOnly(2020, 1, 1),
                To = new DateOnly(2022, 1, 1)
            });

        Assert.Equal(3, requests);
        Assert.Equal(1, response.Csv.Split("t,c", StringSplitOptions.None).Length - 1);
        Assert.Contains("1,101", response.Csv);
        Assert.Contains("3,103", response.Csv);
        Assert.True(response.IsComposite);
        Assert.Equal(3, response.Parts.Count);
        Assert.Equal(response.Csv, response.RawBody);
    }

    [Fact]
    public async Task LongRangeStockCandlesCsv_PreservesFirstHeaderAfterEmptyChunk()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var content = request.RequestUri!.Query.Contains("from=2020-01-01", StringComparison.Ordinal)
                ? string.Empty
                : "t,c\r\n1,101\r\n";
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

        Assert.StartsWith("t,c", response.Csv, StringComparison.Ordinal);
        Assert.Equal(1, response.Csv.Split("t,c", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, response.Csv.Split("1,101", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task SchemaCsvEndpoints_UseExpectedPaths()
    {
        var handler = CreateHandler("value\r\n1\r\n");
        var client = MarketDataTestClient.Create(handler);

        await client.Options.GetStrikesCsvAsync(new OptionsStrikesRequest("AAPL"));
        Assert.Equal("/v1/options/strikes/AAPL/", handler.LastRequest!.RequestUri!.AbsolutePath);

        await client.Stocks.GetPriceCsvAsync(new StockPriceRequest("AAPL"));
        Assert.Equal("/v1/stocks/prices/AAPL/", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    private static StubHttpMessageHandler CreateHandler(string body)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
        });
        handler.ResponseHeaders["x-request-id"] = "request-1";
        handler.ResponseHeaders["x-api-ratelimit-limit"] = "100";
        handler.ResponseHeaders["x-api-ratelimit-remaining"] = "99";
        handler.ResponseHeaders["x-api-ratelimit-reset"] = "1737072000";
        handler.ResponseHeaders["x-api-ratelimit-consumed"] = "1";
        return handler;
    }
}

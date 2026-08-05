using System.Net;
using System.Text;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Configuration;

/// <summary>
/// Exercises the §4 configuration cascade: env / client-level defaults are applied when a
/// per-request <see cref="MarketDataRequestOptions"/> field is null, and a field set on the
/// per-request options wins. Assertions read the emitted query string (JSON path) and the CSV-only
/// headers/human parameters (CSV path).
/// </summary>
public sealed class ConfigurationCascadeTests
{
    [Fact]
    public async Task ClientDefaults_AppliedWhenPerRequestOptionsNull()
    {
        var handler = JsonHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultDateFormat = DateFormat.Timestamp,
            DefaultMode = Mode.Cached,
            DefaultColumns = ["symbol", "mid"]
        });

        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("dateformat=timestamp", query);
        Assert.Contains("mode=cached", query);
        Assert.Contains("columns=symbol%2Cmid", query);
    }

    [Fact]
    public async Task PerRequestOptions_OverrideClientDefaults()
    {
        var handler = JsonHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultDateFormat = DateFormat.Timestamp,
            DefaultMode = Mode.Cached,
            DefaultColumns = ["symbol", "mid"]
        });

        await client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions
            {
                DateFormat = DateFormat.Unix,
                Mode = Mode.Live,
                Columns = ["symbol", "change"]
            });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("dateformat=unix", query);
        Assert.Contains("mode=live", query);
        Assert.Contains("columns=symbol%2Cchange", query);
        Assert.DoesNotContain("dateformat=timestamp", query);
        Assert.DoesNotContain("mode=cached", query);
    }

    [Fact]
    public async Task Cascade_MergesPerFieldSoMethodWinsAndDefaultFillsTheRest()
    {
        var handler = JsonHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultDateFormat = DateFormat.Timestamp,
            DefaultMode = Mode.Cached
        });

        // Only DateFormat is set per request; Mode is left null so the client default fills it.
        await client.Stocks.GetPricesAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions { DateFormat = DateFormat.Unix });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("dateformat=unix", query);
        Assert.Contains("mode=cached", query);
    }

    [Fact]
    public async Task ClientDefaults_AppliedToCsvHeadersAndHuman()
    {
        var handler = CsvHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultAddHeaders = false,
            DefaultHuman = true
        });

        await client.Stocks.GetPricesCsvAsync(new StockPricesRequest("AAPL"));

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("headers=false", query);
        Assert.Contains("human=true", query);
    }

    [Fact]
    public async Task PerRequestOptions_OverrideCsvHeadersAndHuman()
    {
        var handler = CsvHandler();
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            DefaultAddHeaders = false,
            DefaultHuman = true
        });

        await client.Stocks.GetPricesCsvAsync(
            new StockPricesRequest("AAPL"),
            new MarketDataRequestOptions { Headers = true, Human = false });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("headers=true", query);
        Assert.Contains("human=false", query);
    }

    private static StubHttpMessageHandler JsonHandler() =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"s":"ok","symbol":["AAPL"],"mid":[1.0],"change":[0.5]}""",
                Encoding.UTF8,
                "application/json")
        });

    private static StubHttpMessageHandler CsvHandler() =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("symbol,mid\r\nAAPL,1.0\r\n", Encoding.UTF8, "text/csv")
        });
}

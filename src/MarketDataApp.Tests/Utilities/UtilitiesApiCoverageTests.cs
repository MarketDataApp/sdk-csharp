using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Utilities;

public sealed class UtilitiesApiCoverageTests
{
    [Fact]
    public async Task GetStatusAsync_FullPayload_MapsEveryServiceStatusField()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "service": ["/v1/stocks/quotes/"],
          "status": ["online"],
          "online": [true],
          "uptimePct30d": [0.995],
          "uptimePct90d": [0.985],
          "updated": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var status = (await client.Utilities.GetStatusAsync()).Values[0];

        Assert.Equal("/v1/stocks/quotes/", status.Service);
        Assert.Equal("online", status.Status);
        Assert.True(status.Online);
        Assert.Equal(0.995, status.UptimePct30d);
        Assert.Equal(0.985, status.UptimePct90d);
        Assert.NotEqual(default, status.Updated);
        Assert.Equal("/v1/stocks/quotes/ online", status.ToString());
    }

    [Fact]
    public async Task GetStatusAsync_MissingRequiredField_ThrowsParseException()
    {
        // "online" is absent, so the decoder throws "Missing online." wrapped as a ParseException.
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "s": "ok",
          "service": ["/v1/stocks/quotes/"],
          "status": ["online"],
          "uptimePct30d": [0.99],
          "uptimePct90d": [0.98],
          "updated": [1706745600]
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetStatusAsync());
        Assert.Contains("Missing online", exception.InnerException!.Message);
    }

    [Theory]
    [InlineData("service", "Missing service")]
    [InlineData("status", "Missing status")]
    [InlineData("uptimePct30d", "Missing uptimePct30d")]
    [InlineData("uptimePct90d", "Missing uptimePct90d")]
    [InlineData("updated", "Missing updated")]
    public async Task GetStatusAsync_EachMissingRequiredField_ThrowsParseException(
        string omit, string expected)
    {
        var fields = new Dictionary<string, string>
        {
            ["service"] = """["/v1/stocks/quotes/"]""",
            ["status"] = """["online"]""",
            ["online"] = "[true]",
            ["uptimePct30d"] = "[0.99]",
            ["uptimePct90d"] = "[0.98]",
            ["updated"] = "[1706745600]"
        };
        fields.Remove(omit);
        var body = "{\"s\":\"ok\"," + string.Join(",", fields.Select(kv => $"\"{kv.Key}\":{kv.Value}")) + "}";
        var client = MarketDataTestClient.Create(new StubHttpMessageHandler(_ =>
            MarketDataTestClient.JsonResponse(body)));

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetStatusAsync());
        Assert.Contains(expected, exception.InnerException!.Message);
    }

    [Fact]
    public async Task GetHeadersAsync_ParsesHeaderMap()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "Authorization": "Bearer token",
          "User-Agent": "marketdata-sdk"
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Utilities.GetHeadersAsync();

        Assert.Equal("Bearer token", response.Values["Authorization"]);
        Assert.Equal("marketdata-sdk", response.Values["user-agent"]);
        Assert.Equal("/headers/", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUserAsync_ParsesQuotaAndPermissions()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "x-ratelimit-requests-remaining": 99000,
          "x-ratelimit-requests-limit": 100000,
          "x-options-data-permissions": "OPRA data delayed 15 minutes"
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var response = await client.Utilities.GetUserAsync();

        Assert.Equal(99000, response.Values.RequestsRemaining);
        Assert.Equal(100000, response.Values.RequestsLimit);
        Assert.Equal("OPRA data delayed 15 minutes", response.Values.OptionsDataPermissions);
        Assert.Equal("requests 99000/100000", response.Values.ToString());
        Assert.Equal("/user/", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetUserAsync_MissingIntField_ThrowsParseException()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "x-ratelimit-requests-limit": 100000,
          "x-options-data-permissions": ""
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetUserAsync());
        Assert.Contains("x-ratelimit-requests-remaining", exception.InnerException!.Message);
    }

    [Fact]
    public async Task GetUserAsync_MissingStringField_ThrowsParseException()
    {
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "x-ratelimit-requests-remaining": 99000,
          "x-ratelimit-requests-limit": 100000
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetUserAsync());
        Assert.Contains("x-options-data-permissions", exception.InnerException!.Message);
    }

    [Fact]
    public async Task GetUserAsync_OutOfRangeIntField_ThrowsParseException()
    {
        // Present but not representable as an Int32 (exceeds int.MaxValue), so TryGetInt32 fails.
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "x-ratelimit-requests-remaining": 3000000000,
          "x-ratelimit-requests-limit": 100000,
          "x-options-data-permissions": "OPRA"
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetUserAsync());
        Assert.Contains("x-ratelimit-requests-remaining", exception.InnerException!.Message);
    }

    [Fact]
    public async Task GetUserAsync_NonStringStringField_ThrowsParseException()
    {
        // Present but the wrong JSON kind (a number, not a string).
        var handler = new StubHttpMessageHandler(_ => MarketDataTestClient.JsonResponse("""
        {
          "x-ratelimit-requests-remaining": 99000,
          "x-ratelimit-requests-limit": 100000,
          "x-options-data-permissions": 123
        }
        """));
        var client = MarketDataTestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<ParseException>(() => client.Utilities.GetUserAsync());
        Assert.Contains("x-options-data-permissions", exception.InnerException!.Message);
    }
}

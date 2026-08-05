// WebApiSample — Market Data C#/.NET SDK in ASP.NET Core
//
// Demonstrates:
//  - DI-managed HttpClient via IHttpClientFactory
//  - MarketDataClient registered as a singleton service
//  - Configuration loaded from appsettings.json + user secrets
//  - Minimal API endpoints that return live market data
//
// Setup:
//   cd examples/WebApiSample
//   dotnet user-secrets init
//   dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token"
//   dotnet run

using System.ComponentModel;
using System.Diagnostics;
using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Funds;
using MarketDataApp.Options;
using MarketDataApp.Stocks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────────
// In production, supply MARKETDATA_TOKEN through environment variables or a
// secrets manager — never store the token in appsettings.json.
builder.Configuration.AddUserSecrets<Program>(optional: true);

// ── HttpClient via IHttpClientFactory ────────────────────────────────────────────
// Register a named client. The SDK never creates or disposes the HttpClient;
// the factory manages pooling and handler lifetime.
builder.Services.AddHttpClient("MarketData");

// ── MarketDataClient as a singleton ─────────────────────────────────────────────
// A single MarketDataClient is safe to share across concurrent requests.
//
// DI singleton factory delegates are synchronous, so this path uses the plain constructor,
// which performs NO startup token validation and NO network I/O — authentication and
// rate-limit errors surface on the first request (handled by the endpoints below). To
// validate the token and seed the rate-limit snapshot eagerly instead, build the client
// with the async factory `await MarketDataClient.CreateAsync(httpClient, options)` before
// registering it (see examples/McpServer/Program.cs).
builder.Services.AddSingleton<MarketDataClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var config = sp.GetRequiredService<IConfiguration>();

    // FromConfiguration reads all MARKETDATA_* keys:
    //   MARKETDATA_TOKEN, MARKETDATA_BASE_URL,
    //   MARKETDATA_MAX_RETRIES, MARKETDATA_RETRY_BASE_DELAY, MARKETDATA_RETRY_MAX_DELAY,
    //   MARKETDATA_MAX_RETRY_AFTER, MARKETDATA_RETRY_JITTER_FACTOR,
    //   MARKETDATA_MAX_CONCURRENT_REQUESTS, MARKETDATA_API_VERSION, MARKETDATA_USER_AGENT
    var options = MarketDataClientOptions.FromConfiguration(config);
    return new MarketDataClient(factory.CreateClient("MarketData"), options);
});

var app = builder.Build();

// ── Endpoints ────────────────────────────────────────────────────────────────────

string[] samplePaths =
[
    "/quote/AAPL",
    "/price/AAPL",
    "/candles/AAPL?countback=5&resolution=D",
    "/options/chain/AAPL",
    "/funds/candles/VFINX?countback=5&resolution=D",
    "/market/status?country=US",
    "/ratelimit"
];

app.MapGet("/", () => Results.Ok(new { sampleUrls = samplePaths }));

// GET /quote/{symbol}  →  latest real-time quote
app.MapGet("/quote/{symbol}", async (
    string symbol,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var response = await marketData.Stocks.GetQuoteAsync(
            new StockQuoteRequest(symbol),
            cancellationToken: ct);

        return response.IsNoData
            ? Results.NotFound(new { symbol, error = "No data available." })
            : Results.Ok(response.Values);
    }
    catch (AuthenticationException)
    {
        return Results.Unauthorized();
    }
    catch (RateLimitException)
    {
        return Results.StatusCode(429);
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /price/{symbol}  →  single-symbol path-based price
app.MapGet("/price/{symbol}", async (
    string symbol,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var response = await marketData.Stocks.GetPriceAsync(
            new StockPriceRequest(symbol),
            cancellationToken: ct);

        return response.IsNoData
            ? Results.NotFound(new { symbol })
            : Results.Ok(response.Values);
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /candles/{symbol}?countback=5&resolution=D  →  OHLCV candles
app.MapGet("/candles/{symbol}", async (
    string symbol,
    int countback,
    string resolution,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var res = StockResolution.Of(resolution);
        var response = await marketData.Stocks.GetCandlesAsync(
            new StockCandlesRequest(res, symbol) { Countback = countback },
            cancellationToken: ct);

        return response.IsNoData
            ? Results.NotFound(new { symbol })
            : Results.Ok(new
            {
                symbol,
                resolution,
                isComposite = response.IsComposite,
                parts = response.Parts.Count,
                candles = response.Values
            });
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /options/chain/{symbol}  →  options chain
app.MapGet("/options/chain/{symbol}", async (
    string symbol,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var response = await marketData.Options.GetChainAsync(
            new OptionsChainRequest(symbol) { StrikeLimit = 2 },
            cancellationToken: ct);

        return Results.Ok(response.Values);
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /funds/candles/{symbol}?countback=5&resolution=D  →  mutual fund candles
app.MapGet("/funds/candles/{symbol}", async (
    string symbol,
    int countback,
    string resolution,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var response = await marketData.Funds.GetCandlesAsync(
            new FundCandlesRequest(FundResolution.Of(resolution), symbol)
            {
                Countback = countback
            },
            cancellationToken: ct);

        return response.IsNoData
            ? Results.NotFound(new { symbol })
            : Results.Ok(response.Values);
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /market/status?country=US  →  market open/closed status
app.MapGet("/market/status", async (
    string? country,
    MarketDataClient marketData,
    CancellationToken ct) =>
{
    try
    {
        var response = await marketData.Markets.GetStatusAsync(
            new MarketDataApp.Markets.MarketStatusRequest { Country = country ?? "US" },
            cancellationToken: ct);

        return Results.Ok(response.Values);
    }
    catch (MarketDataException ex)
    {
        return Results.Problem(ex.Message, statusCode: ex.StatusCode > 0 ? ex.StatusCode : 502);
    }
});

// GET /ratelimit  →  latest rate-limit snapshot seen by this client
app.MapGet("/ratelimit", (MarketDataClient marketData) =>
    marketData.LatestRateLimit is { } rl
        ? Results.Ok(rl)
        : Results.NoContent());

if (app.Environment.IsDevelopment()
    && !string.Equals(
        app.Configuration["WebApiSample:OpenBrowserOnStart"],
        "false",
        StringComparison.OrdinalIgnoreCase))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;
        var baseAddress = addresses?
            .OrderByDescending(address => address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (baseAddress is null)
        {
            app.Logger.LogWarning("No server address was available to open sample URLs.");
            return;
        }

        foreach (var path in samplePaths)
        {
            var url = new Uri(new Uri(baseAddress.TrimEnd('/') + "/"), path.TrimStart('/'));
            try
            {
                Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Win32Exception exception)
            {
                app.Logger.LogWarning(exception, "Could not open sample URL {SampleUrl}.", url);
            }
            catch (InvalidOperationException exception)
            {
                app.Logger.LogWarning(exception, "Could not open sample URL {SampleUrl}.", url);
            }
        }
    });
}

app.Run();

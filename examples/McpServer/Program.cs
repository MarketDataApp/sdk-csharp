// McpServer - Market Data MCP server over stdio.
//
// Setup:
//   cd examples/McpServer
//   dotnet user-secrets init
//   dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token"
//   dotnet run
//
// MCP clients should launch this project and communicate over standard input/output.
// Logging is routed to stderr so it does not corrupt the MCP JSON-RPC stream.

using MarketDataApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

var httpClient = new HttpClient();
builder.Services.AddSingleton(httpClient);

// Build the shared client with the async factory before the host starts. CreateAsync validates
// the token with /user/ (failing fast on an invalid token) and seeds the rate-limit snapshot,
// without any blocking network I/O. In demo mode (no token) it makes no request. The resulting
// instance is registered as a singleton, which is safe to share across concurrent tool calls.
var marketDataClient = await MarketDataClient.CreateAsync(
    httpClient,
    MarketDataClientOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton(marketDataClient);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MarketDataTools>();

await builder.Build().RunAsync();

[McpServerToolType]
public sealed class MarketDataTools(MarketDataClient marketData)
{
    [McpServerTool, Description("Get the latest quote for a stock symbol.")]
    public async Task<object> GetQuoteAsync(
        [Description("The stock symbol, for example AAPL.")] string symbol,
        CancellationToken cancellationToken)
    {
        var response = await marketData.Stocks.GetQuoteAsync(
            new MarketDataApp.Stocks.StockQuoteRequest(symbol),
            cancellationToken: cancellationToken);

        return new
        {
            symbol,
            noData = response.IsNoData,
            quotes = response.Values
        };
    }

    [McpServerTool, Description("Get OHLCV candles for a stock symbol.")]
    public async Task<object> GetCandlesAsync(
        [Description("The stock symbol, for example MSFT.")] string symbol,
        [Description("The candle resolution, for example D, 60, or 15.")] string resolution,
        [Description("The number of candles to return.")] int countback = 5,
        CancellationToken cancellationToken = default)
    {
        var response = await marketData.Stocks.GetCandlesAsync(
            new MarketDataApp.Stocks.StockCandlesRequest(
                MarketDataApp.Stocks.StockResolution.Of(resolution),
                symbol)
            {
                Countback = countback
            },
            cancellationToken: cancellationToken);

        return new
        {
            symbol,
            resolution,
            noData = response.IsNoData,
            isComposite = response.IsComposite,
            candles = response.Values
        };
    }

    [McpServerTool, Description("Get whether a country's stock market is open.")]
    public async Task<object> GetMarketStatusAsync(
        [Description("The ISO country code, for example US.")] string country = "US",
        CancellationToken cancellationToken = default)
    {
        var response = await marketData.Markets.GetStatusAsync(
            new MarketDataApp.Markets.MarketStatusRequest { Country = country },
            cancellationToken: cancellationToken);

        return new
        {
            country,
            status = response.Values
        };
    }
}

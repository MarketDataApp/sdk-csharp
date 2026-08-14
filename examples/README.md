# Examples

Five runnable samples, from a first request to a hosted service. Each one is a standalone
project referencing the SDK source directly, so `dotnet run` from the example's directory is
all it takes.

| Example | Type | What it shows |
|---|---|---|
| [`QuickStart/`](QuickStart/) | Console | A tour of all five API surfaces: quotes, candles, batched quotes, funds, market calendar, options chain filtering, utilities, CSV export, concurrent fan-out, and the exception taxonomy. |
| [`Watchlist/`](Watchlist/) | Console mini-app | A "real tool": batched quotes for a symbol list on a refresh loop, per-row change coloring, the client-wide rate-limit snapshot on screen, Ctrl+C cancellation, taxonomy-aware error recovery, and a `--export` mode that saves daily candles as CSV. Built on the one-line client setup (the SDK creates and owns its `HttpClient`). |
| [`OptionsChainMonitor/`](OptionsChainMonitor/) | Console mini-app | The options side: a filtered chain (`ExpirationFilter.ForDte`, `strikeLimit`, `OptionSide`) on a refresh loop, rendering bid/ask/mid, volume, open interest, IV, delta, and in-the-money highlighting — plus the chain-specific quota lesson (a chain bills roughly per contract returned, so the defaults stay tight). |
| [`McpServer/`](McpServer/) | Hosted (stdio) | A [Model Context Protocol](https://modelcontextprotocol.io/) server exposing quote, candle, and market-status tools; the shared client is built with `CreateAsync` before the host starts. |
| [`WebApiSample/`](WebApiSample/) | ASP.NET Core | The DI path: `AddMarketDataClient` + `IHttpClientFactory`, minimal-API endpoints mapping the exception taxonomy to HTTP status codes, and a rate-limit endpoint. |

## Token setup (shared by every example)

Most Market Data endpoints require an API token. Each example loads `MARKETDATA_*`
configuration the same way (highest precedence first):

1. Environment variables: `MARKETDATA_TOKEN=your-api-token`
2. A `.env` file in the working directory
3. .NET user secrets, from the example's directory:

```bash
dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token"
```

With a token configured, the client validates it against `/user/` at startup and fails fast
with `AuthenticationException` if it is invalid. Without a token the client starts in demo
mode and authenticated endpoints will fail on first use.

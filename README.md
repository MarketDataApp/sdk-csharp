<div align="center">

# Market Data C#/.NET SDK
### Access Financial Data with Ease

> This is a C#/.NET SDK for [Market Data](https://www.marketdata.app/), built for **C# and Dotnet Core**. It provides developers with a powerful, easy-to-use interface to obtain real-time and historical financial data. Ideal for building financial applications, trading bots, and investment strategies.

#### Connect With The Market Data Community

[![Website](https://img.shields.io/badge/Website-marketdata.app-blue)](https://www.marketdata.app/)
[![Discord](https://img.shields.io/badge/Discord-join%20chat-7389D8.svg?logo=discord&logoColor=ffffff)](https://discord.com/invite/GmdeAVRtnT)
[![Twitter](https://img.shields.io/twitter/follow/MarketDataApp?style=social)](https://twitter.com/MarketDataApp)
[![Helpdesk](https://img.shields.io/badge/Support-Ticketing-ff69b4.svg?logo=TicketTailor&logoColor=white)](https://www.marketdata.app/dashboard/)

[![CI](https://github.com/MarketDataApp/sdk-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/MarketDataApp/sdk-csharp/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/MarketDataApp/sdk-csharp/graph/badge.svg)](https://codecov.io/gh/MarketDataApp/sdk-csharp)
[![Status: Alpha](https://img.shields.io/badge/status-alpha%20(in%20development)-orange)](https://github.com/MarketDataApp/sdk-csharp)

</div>

> [!WARNING]
> **Alpha — in active development. Not ready for public or production use.**
> This SDK has not had a stable release and its public API may change without notice.
> NuGet packages are pre-release (`0.0.0-alpha.*`) until a stable `v1.0.0` is tagged.

## Features

- **Async-first API**: Every endpoint is asynchronous, accepts an optional `CancellationToken`, and returns a typed response
- **Application-owned HTTP**: Inject an existing `HttpClient`, including clients created by `IHttpClientFactory`; the SDK never disposes it
- **Configuration Integration**: Bind options from `IConfiguration`, user-secrets, or environment variables, with explicit programmatic configuration also supported
- **Real-time Stock Data**: Prices, quotes, candles (OHLCV), earnings, and news
- **Options Trading Data**: Options chains, expirations, strikes, quotes, and lookup
- **Mutual Funds**: Historical candles and pricing data
- **Market Status**: Real-time market open/closed status for multiple countries
- **Utilities**: Service status, request-header inspection, and authenticated-user information
- **Multiple Output Formats**: Typed objects, JSON, or CSV
- **Typed Request Models**: Immutable endpoint-specific request objects validate required values and keep endpoint parameters explicit
- **Global Request Options**: `MarketDataRequestOptions` provides shared date-format, mode, limit, offset, column, and CSV formatting controls
- **Response Metadata**: Status codes, request URLs and IDs, raw bodies, no-data indicators, composite parts, and response-saving helpers
- **Resilient Transport**: Per-attempt timeouts, bounded concurrency, fixed exponential backoff, and `Retry-After` support
- **Long Intraday Ranges**: Automatically chunks long intraday stock-candle windows and merges results
- **Built-in Retry Logic**: Automatic retry with exponential backoff for reliable data fetching
- **Rate Limit Tracking**: Per-response and client-level rate-limit snapshots with client-side protection
- **Diagnostics**: `ActivitySource` tracing for manual listeners and OpenTelemetry integration
- **File Export**: Save JSON and CSV response content synchronously or asynchronously
- **Predictable Errors**: A focused exception hierarchy distinguishes authentication, validation, network, parsing, not-found, rate-limit, and server failures
- **Type-Safe**: Records, typed endpoint responses, and idiomatic request objects
- **Zero Config**: Works out of the box with sensible defaults

---

## Contents

- [Installation](#installation)
- [Quick start](#quick-start)
- [Client lifetime and HttpClient injection](#client-lifetime-and-httpclient-injection)
- [Dependency injection (ASP.NET Core)](#dependency-injection-aspnet-core)
- [Configuration](#configuration)
- [Request and response model](#request-and-response-model)
- [Inspecting responses](#inspecting-responses)
- [Documentation](#documentation)
- [Endpoint inventory](#endpoint-inventory)
  - [Stocks](#stocks)
  - [Options](#options)
  - [Funds](#funds)
  - [Markets](#markets)
  - [Utilities](#utilities)
- [CSV responses](#csv-responses)
- [Exception handling](#exception-handling)
- [Retry, timeout, and rate limiting](#retry-timeout-and-rate-limiting)
- [No-data and composite responses](#no-data-and-composite-responses)
- [Diagnostics and tracing](#diagnostics-and-tracing)
- [Integration tests](#integration-tests)
- [SDK design contracts](#sdk-design-contracts)

---

## Installation

```shell
dotnet add package MarketDataApp
```

**Requirements**: .NET 8.0 or newer. The package multi-targets `net8.0` and `net10.0`
(both LTS), so projects on either runtime get a native build.

---

## Quick start

```csharp
// The SDK creates and owns its HttpClient here, pre-configured with the SDK requirements:
// the default handler (2-second connection timeout, §10) plus the fixed 99-second request
// timeout enforced internally. CreateAsync also validates the token with /user/ and seeds
// the rate-limit snapshot at startup; set ValidateTokenOnStartup = false to skip that.
using var client = await MarketDataClient.CreateAsync();
var quote = await client.Stocks.GetQuoteAsync("AAPL");
foreach (var q in quote.Values)
{
    Console.WriteLine($"{q.Symbol}: mid={q.Mid:F2}  last={q.Last:F2}  volume={q.Volume:N0}");
}
```

Managing your own `HttpClient`? Every entry point also accepts one, and the SDK never
reconfigures a supplied client — see
[Client lifetime and HttpClient injection](#client-lifetime-and-httpclient-injection).

See [`examples/`](examples/) for the full index of runnable samples.
See [`examples/QuickStart/`](examples/QuickStart/) for a full runnable example covering
cancellation, CSV export, exception handling, and bulk quotes.
See [`examples/Watchlist/`](examples/Watchlist/) for a console watchlist mini-app: batched
quotes on a refresh loop, a rate-limit footer, taxonomy-aware error recovery, and CSV export.
See [`examples/OptionsChainMonitor/`](examples/OptionsChainMonitor/) for a live filtered
options chain: typed chain filters, IV/delta/open-interest rendering, and chain quota habits.
See [`examples/McpServer/`](examples/McpServer/) for a runnable
[Model Context Protocol](https://modelcontextprotocol.io/) server that exposes quote,
candle, and market-status tools over stdio.
See [`examples/WebApiSample/`](examples/WebApiSample/) for ASP.NET Core DI and
`IHttpClientFactory` patterns. Its Development launch profile opens the AAPL stock,
AAPL options, VFINX fund, US market-status, and rate-limit sample URLs in the browser.
Set `WebApiSample:OpenBrowserOnStart` to `false` to disable this behavior.

---

## Client lifetime and HttpClient injection

Two ownership modes:

- **SDK-owned (default).** The overloads without an `HttpClient` parameter
  (`new MarketDataClient(options)`, `MarketDataClient.CreateAsync(options)`) create a client
  backed by `CreateDefaultHttpHandler()` with the HttpClient-level timeout disabled, because the
  SDK enforces its own fixed 99-second request timeout. `Dispose()` also disposes the owned
  client.
- **Application-owned.** Supply your own `HttpClient` and the application controls the lifetime:
  the SDK never reconfigures it (its handler and `Timeout` are respected as-is) and never
  disposes it.

### Console or background service

```csharp
// Simplest: the SDK creates and owns the HttpClient with the SDK's transport defaults.
using var client = await MarketDataClient.CreateAsync(options);

// Or manage the HttpClient yourself; the SDK uses it as configured and never disposes it.
using var httpClient = new HttpClient(MarketDataClient.CreateDefaultHttpHandler());
var client2 = await MarketDataClient.CreateAsync(httpClient, options);
```

### ASP.NET Core — singleton via IHttpClientFactory

Register the client in one line with
[`AddMarketDataClient`](#dependency-injection-aspnet-core), which wires up the singleton and an
`IHttpClientFactory`-managed `HttpClient` for you:

```csharp
// Program.cs
builder.Services.AddMarketDataClient(builder.Configuration);
```

A single `MarketDataClient` instance is safe to use concurrently from multiple
requests or threads. When a token is configured, the singleton validates it with a blocking
`GET /user/` the first time it is resolved; see
[Dependency injection (ASP.NET Core)](#dependency-injection-aspnet-core) for the overloads,
override behavior, and how to defer validation.

---

## Dependency injection (ASP.NET Core)

The `AddMarketDataClient` service-collection extension registers `MarketDataClient` with one line in
ASP.NET Core or generic-host apps. It lives in `Microsoft.Extensions.DependencyInjection`, so it is
discoverable without an extra `using`:

```csharp
// Program.cs
builder.Services.AddMarketDataClient(builder.Configuration);
```

Then take `MarketDataClient` as a constructor-injected dependency (or a minimal-API parameter):

```csharp
app.MapGet("/quote/{symbol}", async (
    string symbol,
    MarketDataClient client,
    CancellationToken ct) =>
    Results.Ok((await client.Stocks.GetQuoteAsync(symbol, cancellationToken: ct)).Values));
```

`AddMarketDataClient` registers `MarketDataClient` as a **singleton** over an
`IHttpClientFactory`-managed `HttpClient` whose primary handler is the SDK default handler
(`MarketDataClient.CreateDefaultHttpHandler()`), so the 2-second connection timeout applies and
pooled connections rotate DNS; the HttpClient-level timeout is disabled in favor of the SDK's
fixed 99-second request timeout. These are defaults, not enforcement: configure the same named
client (`"MarketDataApp"`) after calling `AddMarketDataClient` and your configuration wins. A
single client instance is safe to use concurrently.

Three overloads resolve the options for you:

| Overload | Options source |
|----------|----------------|
| `AddMarketDataClient()` | `MarketDataClientOptions.FromEnvironment()` — user secrets, `.env`, environment variables |
| `AddMarketDataClient(IConfiguration configuration)` | `MarketDataClientOptions.FromConfiguration(configuration)` — the `MARKETDATA_*` keys |
| `AddMarketDataClient(MarketDataClientOptions options)` | the supplied instance |

Registrations use `TryAdd*`, so you can override either `MarketDataClientOptions` or
`MarketDataClient` by registering your own before calling the extension.

**SDK logs flow automatically.** When your app has logging configured (any provider registered
through `AddLogging` / the host builder), `AddMarketDataClient` auto-wires the container's
`ILogger<MarketDataClient>` into the client, so the SDK's lifecycle, request, response, and error
diagnostics are emitted without any extra wiring. An explicitly supplied `MarketDataClientOptions.Logger`
is always respected and never overridden, and when no logging is configured the client still
constructs silently. Pair it with `builder.Logging.AddMarketDataCanonicalConsole()` to render the
SDK's canonical `{timestamp} - {logger_name} - {level} - {message}` console line.

> **Startup validation at first resolve.** The DI path uses the `MarketDataClient` constructor:
> when a token is configured and `ValidateTokenOnStartup` is `true` (the default), the token is
> validated with a blocking `GET /user/` the first time the singleton is resolved — typically
> during startup wiring — and an invalid token throws `AuthenticationException` at that point.
> Register options with `ValidateTokenOnStartup = false` to defer errors to the first request
> instead.

---

## Configuration

### Storing the token safely

Never commit secrets to source control. Preferred patterns:

**dotnet user-secrets (local development)**

```powershell
dotnet user-secrets init
dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token"
```

**`.env` file (local development)**

Create a `.env` file in the application's current working directory:

```dotenv
MARKETDATA_TOKEN=your-api-token
```

**Environment variables (CI/CD and containers)**

```
MARKETDATA_TOKEN=your-api-token
```

### Loading configuration

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var options = MarketDataClientOptions.FromConfiguration(builder.Configuration);
var client  = new MarketDataClient(httpClient, options);
```

When `MarketDataClient` is created without options, it reads `MARKETDATA_*` keys from
user secrets, `.env`, and environment variables. Precedence is highest for
environment variables, followed by `.env`, then user secrets. `FromConfiguration`
continues to read keys from any configuration provider supplied by the application,
such as user secrets, environment variables, and Azure Key Vault.

### MarketDataClientOptions reference

| Configuration key                     | Property                | Default            | Description |
|---------------------------------------|-------------------------|--------------------|-------------|
| `MARKETDATA_TOKEN`                 | `ApiToken`              | `null`             | Bearer token for authenticated requests. |
| `MARKETDATA_BASE_URL`              | `BaseAddress`           | `https://api.marketdata.app/` | API base URI. |
| `MARKETDATA_API_VERSION`               | `ApiVersion`            | `"v1"`             | Version path segment for versioned endpoints. |
| `MARKETDATA_MAX_RETRIES`               | `MaxRetries`            | `3`                | Retry attempts *after* the original request. Maximum of 4 total attempts. The only configurable retry knob: backoff timing is fixed by the SDK requirements (1s base, doubling per attempt, 30s cap). |
| `MARKETDATA_MAX_CONCURRENT_REQUESTS`    | `MaxConcurrentRequests` | `50`               | Maximum in-flight HTTP requests at one time (semaphore-guarded). |
| `MARKETDATA_USER_AGENT`                | `UserAgent`             | `marketdata-sdk-csharp/{version}` | `User-Agent` header value. |
| `MARKETDATA_DATE_FORMAT`               | `DefaultDateFormat`     | `null`             | Default response date/time format: `unix` / `timestamp` / `spreadsheet`. |
| `MARKETDATA_MODE`                      | `DefaultMode`           | `null`             | Default data mode: `live` / `cached` / `delayed`. |
| `MARKETDATA_COLUMNS`                   | `DefaultColumns`        | `null`             | Default response columns (comma-separated). |
| `MARKETDATA_ADD_HEADERS`               | `DefaultAddHeaders`     | `null`             | Default for the CSV header row (`true`/`false`); CSV requests only. |
| `MARKETDATA_USE_HUMAN_READABLE`        | `DefaultHuman`          | `null`             | Default for human-readable CSV output (`true`/`false`); CSV / human-readable output only. |
| `MARKETDATA_LOGGING_LEVEL`             | `MinimumLogLevel`       | `Information`      | Minimum level for the SDK's own log output: `DEBUG`/`INFO`/`WARNING`/`ERROR` (or a .NET `LogLevel` name). See note. |
| `MARKETDATA_OUTPUT_FORMAT`             | `OutputFormat`          | `null`             | Advisory preferred output: `json` / `csv`. See note. |

All listed `MARKETDATA_*` keys, including retry tuning, `MaxConcurrentRequests`,
`UserAgent`, and the request-formatting defaults, are supported by `FromConfiguration`.
Invalid values throw a `FormatException` naming the offending key. `TimeProvider` is not
configurable via `IConfiguration`; pass it directly to the constructor to replace the
system clock, which is useful in unit tests.

**Configuration cascade.** Request options resolve **per field**: env / client-level
defaults → per-method `MarketDataRequestOptions` params, with the **per-method value
winning**. When a `MarketDataRequestOptions` field is left `null`, the matching client-level
default (`DefaultDateFormat`, `DefaultMode`, `DefaultColumns`, `DefaultAddHeaders`,
`DefaultHuman`) is applied; when the field is set, it overrides the default. `Limit` and
`Offset` have no client-level default and come only from the per-method options.

**`MARKETDATA_OUTPUT_FORMAT` semantics (C#).** `OutputFormat` is advisory /
default-hinting only. The effective output format is determined by *which method you call*:
the typed methods (e.g. `GetQuoteAsync`) return typed models decoded from JSON, and the
paired `*CsvAsync` methods (e.g. `GetQuoteCsvAsync`) return CSV. Configuring this key never
reroutes a typed method to CSV or vice versa.

**`MARKETDATA_LOGGING_LEVEL` semantics (C#).** `MinimumLogLevel` controls the verbosity of
the SDK's own diagnostics sent to the configured `ILogger`; a message is emitted only when
its level is at or above the threshold. The default `Information` suppresses the SDK's Debug
request/response logs unless `DEBUG` is configured.

Startup token validation is on by default on both startup paths and governed by
`ValidateTokenOnStartup`. `await MarketDataClient.CreateAsync(httpClient, options)`
performs an asynchronous `GET /user/` that fails fast on an invalid token (throwing
`AuthenticationException`) and seeds the client-wide rate-limit snapshot before the first
request. The plain constructor `new MarketDataClient(httpClient, options)` runs the same
validation as a blocking request, which makes it the fail-fast path for synchronous hosts
and dependency-injection factories; prefer `CreateAsync` wherever the call site can await.

Set `ValidateTokenOnStartup = false` to skip the startup request and defer validation
until the first authenticated request. In demo mode (no token) neither path makes a
startup request.

Pass an `ILogger` through `Logger`, or use `options.WithLogger(logger)`, to receive
structured SDK diagnostics. Tokens are always redacted in log output.

---

## Request and response model

API Unix timestamps and timestamp strings are returned as `DateTimeOffset` values
normalized to the `America/New_York`/US Eastern time zone.

### Simple endpoint calls

Endpoint methods provide scalar overloads for common calls, so request records are not
required for basic usage:

```csharp
var quote = await client.Stocks.GetQuoteAsync("AAPL");
var candles = await client.Stocks.GetCandlesAsync(
    StockResolution.Daily,
    "AAPL",
    countback: 30);
var chain = await client.Options.GetChainAsync("AAPL");
var status = await client.Markets.GetStatusAsync(country: "US", countback: 5);
```

### Request objects

Use an immutable request record when several optional filters should be grouped or reused.
Required fields are validated in the constructor; optional fields use `init`-only properties:

```csharp
// Required constructor argument
var req = new StockCandlesRequest(StockResolution.Daily, "AAPL")
{
    // Optional init properties
    Countback  = 30,
    Extended   = false,
    AdjustSplits = true
};
```

### MarketDataRequestOptions

All endpoint methods accept an optional `MarketDataRequestOptions` that controls
response formatting:

| Property     | Type                      | Description |
|--------------|---------------------------|-------------|
| `DateFormat` | `DateFormat?`             | Timestamp format in the response. |
| `Mode`       | `Mode?`                   | Requested data freshness (cached/live/etc.). |
| `Limit`      | `int?`                    | Maximum rows returned. |
| `Offset`     | `int?`                    | Rows to skip. |
| `Columns`    | `IReadOnlyList<string>?`  | Columns to include in the response (typed JSON and CSV). |
| `Headers`    | `bool?`                   | CSV output only: include a header row. |
| `Human`      | `bool?`                   | CSV / human-readable output only. Typed JSON always returns typed models by property name, so `human` is never sent on the JSON path. |

### Response objects

Every typed endpoint returns a `MarketDataResponse<T>` subtype with:

| Member           | Type                                | Description |
|------------------|-------------------------------------|-------------|
| `Values`         | `T`                                 | Decoded data payload. |
| `StatusCode`     | `int`                               | HTTP status code. |
| `RequestUrl`     | `Uri`                               | URL that was requested. |
| `RequestId`      | `string?`                           | Server-assigned request ID. |
| `RateLimit`      | `RateLimitSnapshot?`                | Rate-limit info from response headers. |
| `IsNoData`       | `bool`                              | `true` when the API returned no data (empty result). |
| `IsComposite`    | `bool`                              | `true` for chunked multi-request responses. |
| `Parts`          | `IReadOnlyList<MarketDataResponsePart>` | Constituent HTTP responses. |
| `RawBody`        | `string`                            | Raw response body as UTF-8. |
| `SaveToFile`     | method                              | Writes raw body to a file path. |
| `SaveToFileAsync`| method                              | Async version of `SaveToFile`. |

---

## Inspecting responses

Every response wrapper and data record overrides `ToString()`, so logging or printing a
response yields a concise, developer-friendly summary instead of a type name or a raw
payload dump.

```csharp
var quoteResponse = await client.Stocks.GetQuoteAsync("AAPL");

// The response wrapper summarizes its concrete type, item count, and HTTP status.
Console.WriteLine(quoteResponse);
// StockQuotesResponse: 1 item, HTTP 200

// Each data record prints a compact one-line summary.
Console.WriteLine(quoteResponse.Values[0]);
// AAPL mid=150.25 last=150.10

var status = await client.Markets.GetStatusAsync(country: "US", countback: 1);
Console.WriteLine(status.Values[0]);
// 2025-01-10 open
```

CSV and HTML responses summarize their raw size instead of an item count
(e.g. `CsvResponse: 512 bytes, HTTP 200`), and a no-data response appends a marker
(e.g. `StockCandlesResponse: 0 items, HTTP 200, no data`). Missing numeric, string, and
date fields render as `n/a`.

---

## Documentation

Full documentation lives in the [`docs/`](docs/) folder, with a narrative guide per area:

- [Stocks](docs/stocks/README.md)
- [Options](docs/options/README.md)
- [Funds](docs/funds/README.md)
- [Markets](docs/markets/README.md)
- [Utilities](docs/utilities/README.md)

Supporting topics: [installation](docs/installation.md),
[authentication](docs/authentication.md), [client and DI](docs/client.md), and
[settings](docs/settings.md). The [endpoint inventory](#endpoint-inventory) below
summarizes every method, request type, and field.

---

## Endpoint inventory

### Stocks

All stock methods are on `client.Stocks`.

#### Quotes

| Method | Request type | Returns |
|--------|-------------|---------|
| `GetQuoteAsync` | `StockQuoteRequest(symbol)` | `StockQuotesResponse` |
| `GetQuotesAsync` | `StockQuotesRequest(symbols…)` | `StockQuotesResponse` |
| `GetQuoteCsvAsync` | `StockQuoteRequest` | `CsvResponse` |
| `GetQuotesCsvAsync` | `StockQuotesRequest` | `CsvResponse` |

`StockQuote` fields: `Symbol`, `Ask`, `AskSize`, `Bid`, `BidSize`, `Mid`, `Last`,
`Change`, `ChangePct`, `Volume`, `Updated`, `O`, `H`, `L`, `C`, `Week52High`, `Week52Low`.

Optional request fields (`StockQuoteRequest`): `Extended`, `Candle`, `Week52`.

#### Prices

| Method | Request type | Notes |
|--------|-------------|-------|
| `GetPricesAsync` | `StockPricesRequest(symbols…)` | Multi-symbol query endpoint |
| `GetPriceAsync` | `StockPriceRequest(symbol)` | Path-based single-symbol endpoint |
| `GetPricesCsvAsync` | `StockPricesRequest` | |
| `GetPriceCsvAsync` | `StockPriceRequest` | |

`StockPrice` fields: `Symbol`, `Mid`, `Change`, `ChangePct`, `Updated`.

#### Candles

```csharp
// Daily candles — last 30 bars
var resp = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest(StockResolution.Daily, "AAPL") { Countback = 30 });

// 1-minute intraday candles — date range
var resp = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest(StockResolution.Minutes(1), "AAPL")
    {
        From = new DateOnly(2024, 3, 1),
        To   = new DateOnly(2024, 3, 31)
    });
```

`StockResolution` constants: `Daily`, `Weekly`, `Monthly`, `Yearly`.
`StockResolution` factories: `Minutes(n)`, `Hours(n)`, `Days(n)`, `Weeks(n)`, `Months(n)`, `Years(n)`.

Optional fields: `Date`, `From`/`To`, `Countback`, `Exchange`, `Extended`, `Country`,
`AdjustSplits`, `AdjustDividends`. `Date` is exclusive with the range fields; `Countback`
may be combined with `To`, but not with `From`.

`StockCandle` fields: `Time`, `Open`, `High`, `Low`, `Close`, `Volume`.

Long intraday ranges are automatically split into year-sized chunks and merged.
See [No-data and composite responses](#no-data-and-composite-responses).

CSV variant: `GetCandlesCsvAsync`.

#### News

```csharp
var resp = await client.Stocks.GetNewsAsync(
    new StockNewsRequest("AAPL") { Countback = 10 });
```

`StockNewsArticle` fields: `Symbol`, `Headline`, `Content`, `Source`, `PublicationDate`.
CSV variant: `GetNewsCsvAsync`. A `Columns` projection is honored on the typed path too; because
the article fields are non-nullable, a projected subset must still include all five article
columns (the optional `updated` scalar may be dropped).

#### Earnings

```csharp
var resp = await client.Stocks.GetEarningsAsync(
    new StockEarningsRequest("AAPL") { From = new DateOnly(2023, 1, 1) });
```

`StockEarning` fields: `Symbol`, `FiscalYear`, `FiscalQuarter`, `Date`, `ReportDate`,
`ReportTime`, `Currency`, `ReportedEps`, `EstimatedEps`, `SurpriseEps`, `SurpriseEpsPct`, `Updated`.
Optional: `Report` (filter by period), date-window fields.
CSV variant: `GetEarningsCsvAsync`.

---

### Options

All options methods are on `client.Options`.

#### Lookup

Resolves user input (symbol, partial description) to a canonical OCC option symbol.

```csharp
var resp = await client.Options.GetLookupAsync(new OptionsLookupRequest("AAPL 250117C00150000"));
Console.WriteLine(resp.Values); // "AAPL250117C00150000"
```

CSV variant: `GetLookupCsvAsync`.

#### Expirations

```csharp
var resp = await client.Options.GetExpirationsAsync(
    new OptionsExpirationsRequest("AAPL")
    {
        Strike = 150.0m
    });
// resp.Values — IReadOnlyList<DateTimeOffset>
// resp.Updated — DateTimeOffset?
```

Optional fields: `Strike` (filter to expirations that have a contract at this strike),
`Date` (historical as-of date). CSV variant: `GetExpirationsCsvAsync`.

#### Strikes

Returns available strike prices grouped by expiration date.

```csharp
var resp = await client.Options.GetStrikesAsync(
    new OptionsStrikesRequest("AAPL")
    {
        Expiration = new DateOnly(2025, 1, 17)
    });
// resp.Values.Updated — DateTimeOffset? last-updated timestamp
// resp.Values.ByExpiration — IReadOnlyDictionary<DateOnly, IReadOnlyList<double>>
```

Optional fields: `Date` (historical as-of date), `Expiration` (filter to one expiry).
CSV variant: `GetStrikesCsvAsync`.

#### Option quote (single symbol)

```csharp
var resp = await client.Options.GetQuoteAsync(
    new OptionsQuoteRequest("AAPL250117C00150000")
    {
        From = new DateOnly(2025, 1, 2),
        To   = new DateOnly(2025, 1, 10)
    });
```

Optional date-window fields: `Date`, `From`/`To`.
`OptionsQuotesResponse.Values` — `IReadOnlyList<OptionQuote>`.
CSV variant: `GetQuoteCsvAsync`.

#### Option quotes (multiple symbols)

```csharp
// Returns one OptionsQuotesResponse per symbol, fetched concurrently.
IReadOnlyDictionary<string, OptionsQuotesResponse> results =
    await client.Options.GetQuotesAsync(
        new OptionsQuotesRequest("AAPL250117C00150000", "AAPL250117P00150000"));
```

#### Options chain

```csharp
var resp = await client.Options.GetChainAsync(
    new OptionsChainRequest("AAPL")
    {
        Expiration  = ExpirationFilter.ForDte(30),  // ~30 days to expiry
        Side        = OptionSide.Call,
        StrikeLimit = 5,
        MinVolume   = 100
    });
// resp.Values — IReadOnlyList<OptionQuote>
```

`OptionsChainRequest` filters: `Expiration`, `Weekly`, `Monthly`, `Quarterly`, `Am`, `Pm`,
`NonStandard`, `Strike`, `Delta`, `StrikeLimit`, `StrikeRangeFilter`, `MinBid`, `MaxBid`,
`MinAsk`, `MaxAsk`, `MaxBidAskSpread`, `MaxBidAskSpreadPct`, `MinOpenInterest`, `MinVolume`,
`Side`, `Date`.

CSV variant: `GetChainCsvAsync`.

`OptionQuote` key fields: `OptionSymbol`, `Underlying`, `Expiration`, `Strike`, `Side`,
`Bid`, `Ask`, `Mid`, `Last`, `Volume`, `OpenInterest`, `InTheMoney`, `IV`, `Delta`,
`Gamma`, `Theta`, `Vega`, `Rho`, `UnderlyingPrice`, `Updated`.

---

### Funds

```csharp
// Typed candles
var resp = await client.Funds.GetCandlesAsync(
    new FundCandlesRequest(FundResolution.Daily, "SPY")
    {
        Countback = 20
    });
// resp.Values — IReadOnlyList<FundCandle>  (Time, Open, High, Low, Close — no volume)

// CSV variant
var csv = await client.Funds.GetCandlesCsvAsync(
    new FundCandlesRequest(FundResolution.Daily, "SPY") { Countback = 20 });
```

---

### Markets

```csharp
var resp = await client.Markets.GetStatusAsync(new MarketStatusRequest
{
    Country  = "US",
    Countback = 5
});
// resp.Values — IReadOnlyList<MarketStatus> (Date, Status string)

// CSV variant
var csv = await client.Markets.GetStatusCsvAsync(new MarketStatusRequest { Country = "US" });
```

All date-window fields (`Date`, `From`/`To`, `Countback`) are optional. `Date` is exclusive
with the range fields; `Countback` may be combined with `To`, but not with `From`.
`Country` must be a two-letter ISO 3166 code when supplied; defaults to `"US"`.

---

### Utilities

```csharp
// API service status (does not require an API token)
var status = await client.Utilities.GetStatusAsync();
// status.Values — IReadOnlyList<ServiceStatus>

// Request headers observed by the API
var headers = await client.Utilities.GetHeadersAsync();
// headers.Values — IReadOnlyDictionary<string, string>

// Authenticated user quota and entitlements
var user = await client.Utilities.GetUserAsync();
// user.Values — User (RequestsRemaining, RequestsLimit, OptionsDataPermissions)
```

---

## CSV responses

Every typed endpoint has a `Get*CsvAsync` counterpart. CSV responses expose the raw
text through `Values`, `Csv`, and `RawBody` (all equivalent):

```csharp
var response = await client.Stocks.GetPricesCsvAsync(
    new StockPricesRequest("AAPL", "MSFT"),
    new MarketDataRequestOptions
    {
        Headers = true,
        Human   = true,
        Columns = ["symbol", "mid"]
    });

File.WriteAllText("prices.csv", response.Csv);
```

`CsvResponse` carries the same metadata as typed responses: `StatusCode`, `RequestUrl`,
`RequestId`, `RateLimit`, `IsNoData`, `IsComposite`, and `Parts`.

---

## Exception handling

All SDK exceptions derive from `MarketDataApp.Exceptions.MarketDataException`.
The closed hierarchy is:

| Exception | StatusCode | When thrown |
|-----------|-----------|-------------|
| `AuthenticationException` | 401 or 403 | Invalid or missing token |
| `BadRequestException` | 400 | Invalid request parameters |
| `NotFoundException` | 404 | Reserved for non-data resources that report not found |
| `RateLimitException` | 429 | Quota exhausted; `RetryAfter` is populated when server supplies `Retry-After` |
| `ServerException` | 5xx | Upstream server error; `RetryAfter` may be populated |
| `NetworkException` | 0 | Transport failure or per-attempt timeout |
| `ParseException` | varies | Response body could not be decoded |

Every exception exposes:
- `Message` — human-readable description
- `StatusCode` — HTTP status code (0 for network errors)
- `RequestUrl` — URL that was requested
- `RequestId` — server-assigned ID for support tickets
- `Timestamp` — when the exception was created
- `SupportInfo` — pre-formatted support block with all fields

```csharp
try
{
    var resp = await client.Stocks.GetQuoteAsync(new StockQuoteRequest("AAPL"), cancellationToken: ct);
}
catch (OperationCanceledException)
{
    // caller cancellation; SDK timeouts are NetworkException
}
catch (RateLimitException ex)
{
    var wait = ex.RetryAfter?.TotalSeconds;
    Console.Error.WriteLine($"Rate limited. Retry after {wait}s.");
}
catch (AuthenticationException ex)
{
    Console.Error.WriteLine($"Auth failed: {ex.Message}");
}
catch (MarketDataException ex)
{
    // Catch-all for all other SDK errors
    Console.Error.WriteLine(ex.SupportInfo);
}
```

`SupportInfo` renders a fixed, column-aligned block for support tickets:

```text
--- MARKET DATA SUPPORT INFO ---
request_id:     BOAxvA
request_url:    https://api.marketdata.app/v1/stocks/quotes/AAPL/
status_code:    429
timestamp:      2026-08-04 09:30:00
message:        Rate limit exceeded
exception_type: RateLimitException
--------------------------------
```

---

## Retry, timeout, and rate limiting

### Request and connection timeouts

The SDK enforces a **fixed 99-second request timeout** independently on **each HTTP
attempt**. It is intentionally not configurable. A `NetworkException` is thrown if the
per-attempt deadline is exceeded, and caller `CancellationToken` cancellation remains
distinguishable from an SDK timeout (`OperationCanceledException` vs `NetworkException`).

A separate, shorter **2-second connection timeout** (the TCP + TLS handshake) is a
property of the `HttpClient` handler. The SDK-owned client (the overloads without an
`HttpClient` parameter) and the DI-registered named client apply it automatically through
`CreateDefaultHttpHandler()`, with the HttpClient-level timeout disabled so it cannot race
the SDK's 99-second policy. When you manage your own `HttpClient`, opt in by backing it
with the handler factory:

```csharp
// The handler supplies the 2-second connect timeout; the SDK still enforces the
// fixed 99-second request timeout on every attempt.
using var httpClient = new HttpClient(MarketDataClient.CreateDefaultHttpHandler());
var client = await MarketDataClient.CreateAsync(httpClient);
```

`CreateDefaultHttpHandler()` returns a `SocketsHttpHandler` with
`ConnectTimeout = TimeSpan.FromSeconds(2)` plus sensible connection-pool defaults. If you
already configure your own handler, set `SocketsHttpHandler.ConnectTimeout` to
`TimeSpan.FromSeconds(2)` directly instead.

A caller-managed `HttpClient` is never reconfigured, so its own `Timeout` (100 seconds by
default) stays in effect and may fire before the SDK's 99-second deadline. When it does,
the failure surfaces as a `NetworkException` like any other timeout, never as a raw
`TaskCanceledException`.

### Automatic retries

The following failures trigger automatic retries up to `MaxRetries` times (default 3):

- Transport failures (`NetworkException`)
- HTTP 501–599 (server failures)

HTTP 400, 401, 403, 404, 408, 429, 500, and parse errors are **never** retried.

Retry delay uses fixed exponential backoff: 1 second, then 2, then 4. When the server
supplies a `Retry-After` header, that value takes precedence and is honored up to an
internal 10-minute cap.

### Client-side rate-limit short-circuit

Before every request, the SDK checks the latest stored `RateLimitSnapshot`. If the
quota is exhausted and the reset timestamp is in the future, a `RateLimitException` is
thrown immediately without hitting the network.

### Inspecting rate limits

```csharp
// Per-response snapshot
if (response.RateLimit is { } rl)
{
    Console.WriteLine($"{rl.Remaining}/{rl.Limit} requests remaining, resets {rl.Reset:HH:mm} UTC");
}

// Client-wide latest snapshot
if (client.LatestRateLimit is { } rl)
{
    Console.WriteLine($"Consumed: {rl.Consumed}");
}
```

### Concurrency

A single `MarketDataClient` is safe to use concurrently. A built-in semaphore limits
simultaneous in-flight HTTP requests to `MaxConcurrentRequests` (default 50).

---

## No-data and composite responses

### No-data

When the API returns a valid response with an empty result (e.g., a holiday or
out-of-range date), `response.IsNoData` is `true` and `response.Values` is an empty
collection. A `NotFoundException` is **not** thrown in this case.

```csharp
var resp = await client.Stocks.GetCandlesAsync(req);
if (resp.IsNoData)
{
    Console.WriteLine("No candles available for the requested range.");
    return;
}
```

### Composite responses (chunked candles)

Long intraday stock-candle requests spanning more than one year are split into
year-sized chunks. The merged response exposes every constituent request via `Parts`:

```csharp
var resp = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest(StockResolution.Minutes(5), "AAPL")
    {
        From = new DateOnly(2022, 1, 1),
        To   = new DateOnly(2024, 12, 31)
    });

Console.WriteLine($"Composite: {resp.IsComposite}   Parts: {resp.Parts.Count}");
foreach (var part in resp.Parts)
{
    Console.WriteLine($"  {part.RequestUrl}  status={part.StatusCode}");
}
```

For a typed composite response, top-level `RequestId` and `RateLimit` are `null` and
`RawBody` is empty because no single server response represents the merged payload.
For composite CSV, `RawBody` contains the merged CSV. Use `Parts` for each request's
URL, request ID, rate-limit snapshot, status, and raw response body.

---

## Diagnostics, logging, and tracing

The SDK accepts any `Microsoft.Extensions.Logging.ILogger` implementation:

```csharp
var options = new MarketDataClientOptions
{
    ApiToken = token,
    Logger = logger
};
var client = new MarketDataClient(httpClient, options);
```

The `WithLogger` extension provides the equivalent fluent form:

```csharp
var client = new MarketDataClient(httpClient, options.WithLogger(logger));
```

It logs initialization, redacted token configuration, request/response details, retries,
and failures without logging credentials.

### Log format (structured events vs. the canonical text layout)

The SDK does **not** print its own log lines or hard-code a text layout. Instead it emits
**structured `Microsoft.Extensions.Logging` events** — the idiomatic .NET approach — with
levels, named message properties (URL, status code, retry count, redacted token suffix, …)
already applied. The rendered text layout is the **logging provider's** responsibility, so
you can send the same events to the console, JSON, Seq, OpenTelemetry, etc. without the SDK
changing.

To render the SDK's events in the canonical
`{timestamp} - {logger_name} - {level} - {message}` text layout, configure a provider with
a matching output template. For example, with Serilog:

```csharp
using Serilog;
using Serilog.Extensions.Logging;

// "{Timestamp} - {SourceContext} - {Level} - {Message}" == the canonical layout.
var serilog = new LoggerConfiguration()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} - {SourceContext} - {Level:u} - {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Hand the SDK an ILogger that writes through that template.
var logger = new SerilogLoggerFactory(serilog).CreateLogger("MarketDataApp");
var client = new MarketDataClient(httpClient, options.WithLogger(logger));
```

With the built-in `Microsoft.Extensions.Logging.Console` provider, `AddSimpleConsole`
(optionally with `TimestampFormat`) or a custom `ConsoleFormatter` produces the equivalent
layout; the events and their properties are identical regardless of the provider.

For an exact match to the canonical layout without hand-authoring a template, the SDK ships
an **opt-in** console formatter. Call `AddMarketDataCanonicalConsole()` on your logging
builder and the built-in console provider renders each event as the canonical line:

```csharp
builder.Logging.AddMarketDataCanonicalConsole();
// Output: 2025-02-21 12:00:00 - marketdata.client - INFO - Making request...
```

The timestamp is US/Eastern (`yyyy-MM-dd HH:mm:ss`) and the level is mapped to the spec
vocabulary (`DEBUG`/`INFO`/`WARNING`/`ERROR`). This is purely opt-in; by default the SDK
emits structured events and leaves formatting to the provider you attach.

The SDK emits `System.Diagnostics.Activity` spans via `MarketDataDiagnostics.ActivitySource`.

| Name | `ActivitySource.Name` value |
|------|-----------------------------|
| `ActivitySourceName` | `"MarketDataApp.SDK"` |

| Activity name | Kind | Tags |
|---------------|------|------|
| `marketdata.http.get` | Client | `http.request.method`, `url.full` (query stripped), `http.response.status_code`, `marketdata.request_id` |
| `marketdata.retry` | Internal | `marketdata.retry.count`, `marketdata.retry.delay_ms`, `error.type` |

### OpenTelemetry integration

```csharp
// Add to your tracer configuration:
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(MarketDataDiagnostics.ActivitySourceName)
        .AddOtlpExporter());
```

### Manual listener (no OpenTelemetry SDK)

```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == MarketDataDiagnostics.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = a => Console.WriteLine($"[{a.DisplayName}] {a.Duration.TotalMilliseconds:F1}ms")
};
ActivitySource.AddActivityListener(listener);
```

---

## Integration tests

The integration test project `src/MarketDataApp.IntegrationTests` requires a live API
token and uses standard .NET configuration. An environment-variable gate keeps it out
of default local test runs; CI enables the gate explicitly where the suite must run.

### Environment variables

| Configuration | Value | Description |
|---------------|-------|-------------|
| `MARKETDATA_RUN_INTEGRATION_TESTS` | `"true"` | Safety gate that enables live tests. |
| `MARKETDATA_TOKEN` | your token | Standard .NET configuration key loaded from user-secrets or another provider. |

### Running locally

```powershell
dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token" `
  --project src/MarketDataApp.IntegrationTests
$env:MARKETDATA_RUN_INTEGRATION_TESTS = "true"
try {
    dotnet test src/MarketDataApp.IntegrationTests/MarketDataApp.IntegrationTests.csproj
}
finally {
    Remove-Item Env:MARKETDATA_RUN_INTEGRATION_TESTS -ErrorAction SilentlyContinue
}
```

### Running in CI

The `integration` job in `.github/workflows/ci.yml` runs the live suite automatically
on every pull request and on published releases, and can additionally be triggered on
demand via the **Run live integration tests** checkbox on manual workflow dispatch. It
is never triggered by ordinary pushes. The job sets
`MARKETDATA_RUN_INTEGRATION_TESTS=true` and maps the `MARKETDATA_TOKEN` repository
secret into the environment; on fork PRs, which cannot read repository secrets, the
`IntegrationFact` guard skips the individual tests instead of failing.

---

## SDK design contracts

The following contracts are locked for the 1.0 design. Planned behavior that is not implemented
yet is identified explicitly.

### HTTP client and asynchronous behavior

- The application can inject and own `HttpClient` (the SDK never reconfigures or disposes a
  supplied client), or omit it and let the SDK create, configure, and dispose its own over the
  default handler.
- Public endpoint methods are asynchronous and accept `CancellationToken`.
- A fixed, non-configurable 99-second request timeout applies independently to each HTTP
  attempt. Caller cancellation remains distinguishable from an SDK timeout. The separate
  2-second connection timeout comes from the default handler — automatic on SDK-owned and DI
  clients, opt-in via `MarketDataClient.CreateDefaultHttpHandler()` for caller-owned ones. A
  caller-configured `HttpClient.Timeout` is respected and surfaces as `NetworkException` when
  it fires first.
- Endpoint requests are HTTP `GET` operations. Automatic retries never apply to parsing,
  authentication, validation, or other deterministic failures.

### Retry behavior

- `MaxRetries` means retries after the original attempt. The default of `3` therefore permits
  at most four HTTP attempts.
- Transport failures, HTTP 408, HTTP 429, and HTTP 5xx responses are eligible for retry.
- `Retry-After` takes precedence over exponential backoff when supplied by the server.
- The retry loop and backoff honor caller cancellation.
- Exponential backoff is fixed (1 second base, doubling per attempt, 30-second cap);
  server-provided `Retry-After` is honored up to an internal 10-minute cap.

### Concurrency

- A single `MarketDataClient` is safe to use concurrently.
- Operations that fan out internally share the client-wide `MaxConcurrentRequests` limit,
  which defaults to 50.
- A fan-out operation fails if any constituent request fails; successful partial results are not
  returned as a successful response.

### Chunked response metadata

Long intraday stock-candle requests are logical requests composed of multiple HTTP requests.
The response preserves merged values and exposes metadata for every constituent request through
`Parts`; aggregate fields do not misrepresent one constituent as the complete logical response.

### API contract sources

The live [OpenAPI schema](https://api.marketdata.app/schema/) is the primary source for documented
endpoint paths and wire parameters. Existing Funds and Utilities behavior is retained even though
those endpoints are currently absent from that schema.

Options strikes, bulk stock quotes, and the single-symbol stock-price route are implemented.
The bulk-candles definition declares a required `symbol` path parameter that is absent from its
path template, so that endpoint remains deferred until its production contract is confirmed.

# Settings and request options

## Client options

`MarketDataClientOptions.FromConfiguration(IConfiguration)` reads these application
keys. Transport keys:

| Key | Property | Default |
|---|---|---|
| `MARKETDATA_TOKEN` | `ApiToken` | `null` |
| `MARKETDATA_BASE_URL` | `BaseAddress` | `https://api.marketdata.app/` |
| `MARKETDATA_API_VERSION` | `ApiVersion` | `v1` |
| `MARKETDATA_MAX_RETRIES` | `MaxRetries` | 3 retries |
| `MARKETDATA_RETRY_BASE_DELAY` | `RetryBaseDelay` | `00:00:01` |
| `MARKETDATA_RETRY_MAX_DELAY` | `RetryMaxDelay` | `00:00:30` |
| `MARKETDATA_MAX_RETRY_AFTER` | `MaxRetryAfter` | `00:10:00` |
| `MARKETDATA_RETRY_JITTER_FACTOR` | `RetryJitterFactor` | `0` |
| `MARKETDATA_MAX_CONCURRENT_REQUESTS` | `MaxConcurrentRequests` | 50 |
| `MARKETDATA_USER_AGENT` | `UserAgent` | `marketdata-sdk-csharp/{version}` |

Request-formatting defaults and diagnostics. The formatting keys seed client-level
defaults for the per-request `MarketDataRequestOptions` fields of the same name
(see the cascade below):

| Key | Property | Default | Notes |
|---|---|---|---|
| `MARKETDATA_DATE_FORMAT` | `DefaultDateFormat` | `null` | `unix` / `timestamp` / `spreadsheet`. |
| `MARKETDATA_MODE` | `DefaultMode` | `null` | `live` / `cached` / `delayed`. |
| `MARKETDATA_COLUMNS` | `DefaultColumns` | `null` | Comma-separated column list. |
| `MARKETDATA_ADD_HEADERS` | `DefaultAddHeaders` | `null` | `true` / `false`; applies to CSV requests only. |
| `MARKETDATA_USE_HUMAN_READABLE` | `DefaultHuman` | `null` | `true` / `false`; applies to CSV / human-readable output only. |
| `MARKETDATA_LOGGING_LEVEL` | `MinimumLogLevel` | `Information` | `DEBUG` / `INFO` / `WARNING` / `ERROR` (or a .NET `LogLevel` name). Controls the SDK's own log verbosity. |
| `MARKETDATA_OUTPUT_FORMAT` | `OutputFormat` | `null` | `json` / `csv`. Advisory / default-hinting only (see note). |

Invalid values for any key throw a `FormatException` naming the offending key.

### `MARKETDATA_OUTPUT_FORMAT` (C# semantics)

`OutputFormat` is **advisory / default-hinting only**. In this SDK the effective output
format is chosen by *which method you call*: the typed endpoint methods (e.g.
`GetQuoteAsync`) return typed models decoded from JSON, while the paired `*CsvAsync`
methods (e.g. `GetQuoteCsvAsync`) return CSV. Configuring `MARKETDATA_OUTPUT_FORMAT`
stores the hint on `OutputFormat` but never reroutes a typed method to CSV or vice versa.

### `MARKETDATA_LOGGING_LEVEL` (C# semantics)

`MinimumLogLevel` controls the verbosity of the **SDK's own** diagnostics emitted through
the configured `ILogger`. A message is emitted only when its level is at or above the
threshold. The default `Information` therefore suppresses the SDK's Debug request/response
logs unless `MARKETDATA_LOGGING_LEVEL=DEBUG` is configured.

### Configuration cascade

Request options resolve **per field** in this order: env / client-level defaults →
per-method `MarketDataRequestOptions` params, with the **per-method value winning**. When a
`MarketDataRequestOptions` field is left `null`, the matching client-level default
(`DefaultDateFormat`, `DefaultMode`, `DefaultColumns`, `DefaultAddHeaders`, `DefaultHuman`)
is applied; when the field is set, it overrides the default. `Limit` and `Offset` have no
client-level default and are taken only from the per-method options.

```csharp
// MARKETDATA_DATE_FORMAT=timestamp, MARKETDATA_MODE=cached configured in the environment.
var options = MarketDataClientOptions.FromEnvironment();
using var client = new MarketDataClient(httpClient, options);

// dateformat=timestamp and mode=cached are applied from the client defaults.
await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

// dateformat=unix wins from the per-method options; mode=cached still fills from the default.
await client.Stocks.GetPricesAsync(
    new StockPricesRequest("AAPL"),
    new MarketDataRequestOptions { DateFormat = DateFormat.Unix });
```

## Default configuration sources

`new MarketDataClient(httpClient)` loads configuration automatically from the
following sources, in increasing precedence order:

1. .NET user secrets (lowest priority)
2. An optional `.env` file in the current working directory
3. Environment variables (highest priority)

For example:

```dotenv
MARKETDATA_TOKEN=your-api-token
MARKETDATA_BASE_URL=https://api.marketdata.app/
```

Environment variables override matching values from `.env` and user secrets. Do not
commit `.env` files containing secrets.

Advanced retry delays, jitter, `TimeProvider`, and `UserAgent` are configured
programmatically:

```csharp
var options = new MarketDataClientOptions
{
    ApiToken = token,
    Timeout = TimeSpan.FromSeconds(30),
    MaxRetries = 2,
    RetryBaseDelay = TimeSpan.FromMilliseconds(250),
    RetryMaxDelay = TimeSpan.FromSeconds(10),
    MaxRetryAfter = TimeSpan.FromMinutes(2),
    RetryJitterFactor = 0.2,
    TimeProvider = TimeProvider.System,
    UserAgent = "my-app/1.0"
};
```

## Simple endpoint calls

Endpoint methods support scalar parameters for common calls:

```csharp
var quote = await client.Stocks.GetQuoteAsync("AAPL");
var candles = await client.Stocks.GetCandlesAsync(
    StockResolution.Daily,
    "AAPL",
    countback: 30);
```

## Request objects

Use request records when several optional filters should be grouped or reused. Required
values are constructor arguments; optional values use `init` properties:

```csharp
var request = new StockCandlesRequest(StockResolution.Daily, "AAPL")
{
    Countback = 30,
    AdjustDividends = true
};
```

## MarketDataRequestOptions

Pass an optional `MarketDataRequestOptions` to any endpoint:

```csharp
var response = await client.Stocks.GetQuotesAsync(
    new StockQuotesRequest("AAPL", "MSFT"),
    new MarketDataRequestOptions
    {
        DateFormat = DateFormat.Timestamp,
        Mode = Mode.Delayed,
        Limit = 50,
        Columns = ["symbol", "last"]
    },
    cancellationToken);
```

`DateFormat` and `Columns` apply to both typed JSON and CSV requests. `Headers` and `Human`
apply to CSV / human-readable output only: typed JSON responses always return typed models
keyed by property name (the array-keyed JSON decoder requires the machine field names), so
`human` is never sent on the JSON request path regardless of this flag.

## Response metadata and files

Every response provides `StatusCode`, `RequestUrl`, `RequestId`, `RateLimit`,
`IsNoData`, `IsComposite`, and `Parts`. The raw body can be accessed through
`RawBody` or saved:

```csharp
await response.SaveToFileAsync("quotes.json", cancellationToken);
```

CSV responses expose the same text through `Values`, `Csv`, and `RawBody`.

# Client

`MarketDataClient` is the entry point for five API surfaces:
`Stocks`, `Options`, `Funds`, `Markets`, and `Utilities`.

## HttpClient ownership

The application injects and owns `HttpClient`. `MarketDataClient` does not dispose it.
In a console application, create the client with the async factory so the token is
validated and the rate-limit snapshot is seeded at startup:

```csharp
using var httpClient = new HttpClient();
var client = await MarketDataClient.CreateAsync(httpClient);
```

The plain constructor `new MarketDataClient(httpClient)` performs no network I/O and no
startup validation; authentication and rate-limit errors surface on the first request.
See [authentication](authentication.md#startup-token-validation) for details.

When `options` is omitted, the client loads configuration from user secrets, an
optional `.env` file in the current working directory, and process environment
variables. Environment variables have the highest precedence, followed by `.env`,
then user secrets.

In ASP.NET Core (or any generic host), register the client with the
`AddMarketDataClient` service-collection extension. It lives in
`Microsoft.Extensions.DependencyInjection`, so it is discoverable without an extra
`using`:

```csharp
builder.Services.AddMarketDataClient(builder.Configuration);
```

Then take `MarketDataClient` as a constructor-injected dependency (or a minimal-API
parameter). `AddMarketDataClient` registers `MarketDataClient` as a **singleton** over an
`IHttpClientFactory`-managed `HttpClient` whose primary handler is
`MarketDataClient.CreateDefaultHttpHandler()` (2-second connect timeout, pooled-connection
rotation). Three overloads resolve the options:

| Overload | Options source |
|----------|----------------|
| `AddMarketDataClient()` | `MarketDataClientOptions.FromEnvironment()` (user secrets, `.env`, env vars) |
| `AddMarketDataClient(IConfiguration configuration)` | `MarketDataClientOptions.FromConfiguration(configuration)` (the `MARKETDATA_*` keys) |
| `AddMarketDataClient(MarketDataClientOptions options)` | the supplied instance |

Registrations use `TryAdd*`, so an application can override either the options or the
client by registering its own before calling the extension.

The DI path uses the `MarketDataClient` constructor and performs **no eager startup token
validation** — authentication and rate-limit errors surface on the first request. For
fail-fast startup validation, build the client with `await MarketDataClient.CreateAsync(...)`
and register that instance instead.

A single `MarketDataClient` is safe to use concurrently. Its
`MaxConcurrentRequests` option limits in-flight requests, including internal fan-out.

## Async and cancellation

Endpoint methods are async-only. Every endpoint accepts a `CancellationToken`:

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

var response = await client.Stocks.GetQuoteAsync(
    "AAPL",
    cancellationToken: timeout.Token);

// Request objects remain available for grouped optional filters.
var candles = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest(StockResolution.Daily, "AAPL")
    {
        Countback = 30
    },
    cancellationToken: timeout.Token);
```

Caller cancellation produces `OperationCanceledException`. The configured
`MarketDataClientOptions.Timeout` applies separately to each HTTP attempt; an SDK
timeout is surfaced as `NetworkException`.

## Client-wide rate limits

After a response, `client.LatestRateLimit` contains the latest complete snapshot, or
`null` before a response has supplied rate-limit headers:

```csharp
if (client.LatestRateLimit is { } limit)
{
    Console.WriteLine($"{limit.Remaining}/{limit.Limit} remaining");
}
```

Per-response metadata is available through `response.RateLimit`.

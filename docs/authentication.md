# Authentication

Most Market Data requests require an API token. Never commit a token, put it in a
sample, or print it in logs. Use user-secrets for local development and an environment
variable or managed secret provider in deployed applications.

## Configuring API token

You can create a client without passing options:

```csharp
using MarketDataApp;

using var httpClient = new HttpClient();
var client = new MarketDataClient(httpClient);
```

When no options are supplied, the SDK loads `MARKETDATA_*` values from these sources:

1. Environment variables (highest priority)
2. `.env` file in the assembly's working directory
3. .NET user secrets

For example, a local `.env` file can contain:

```dotenv
MARKETDATA_TOKEN=your-api-token
```

Environment variables override values from both `.env` and user secrets. The `.env`
file is intended for local development and should not be committed.

### User secrets

From an executable project:

```powershell
dotnet user-secrets init
dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token"
```

## Explicit options

Explicit options are useful in tests or short-lived tools. Keep the token outside
source control:

```csharp
var options = new MarketDataClientOptions
{
    ApiToken = Environment.GetEnvironmentVariable("MARKETDATA_TOKEN")
};
```

`ApiToken` may be `null` for unauthenticated/free requests, but authenticated endpoints
can then throw `AuthenticationException`.

## Startup token validation

Use the async factory to validate the token when the client is created:

```csharp
using var httpClient = new HttpClient();
var client = await MarketDataClient.CreateAsync(
    httpClient,
    new MarketDataClientOptions { ApiToken = "your-api-token" });
```

`CreateAsync` performs a single `GET /user/` that:

1. fails fast on an invalid token by throwing `AuthenticationException`, and
2. seeds the client-wide rate-limit snapshot (`client.LatestRateLimit`) before the first
   data request.

Startup validation is on by default and governed by
`MarketDataClientOptions.ValidateTokenOnStartup`. Set it to `false` for short-lived
processes that prefer first-request (lazy) validation, or when no token is configured
(demo mode), in which case `CreateAsync` returns immediately without a request.

The plain constructor performs no network I/O and no startup validation:

```csharp
// No startup validation — auth and rate-limit errors surface on the first request.
var client = new MarketDataClient(httpClient, options);
```

This constructor is the idiomatic no-validation path and is also the right choice for
synchronous dependency-injection factories, which cannot `await`.

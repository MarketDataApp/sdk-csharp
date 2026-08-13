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

When an API token is configured, the client validates it at startup by default — on both
construction paths. The startup `GET /user/`:

1. fails fast on an invalid token by throwing `AuthenticationException`, and
2. seeds the client-wide rate-limit snapshot (`client.LatestRateLimit`) before the first
   data request.

In asynchronous applications, prefer the async factory, which runs the validation without
blocking the calling thread:

```csharp
using var httpClient = new HttpClient();
var client = await MarketDataClient.CreateAsync(
    httpClient,
    new MarketDataClientOptions { ApiToken = "your-api-token" });
```

The plain constructor runs the same validation as a blocking request, which makes it the
fail-fast path for synchronous hosts and dependency-injection factories, which cannot
`await`:

```csharp
// Validates the token with a blocking GET /user/ when a token is configured.
var client = new MarketDataClient(httpClient, options);
```

Startup validation is governed by `MarketDataClientOptions.ValidateTokenOnStartup`. Set
it to `false` for first-request (lazy) validation with no startup network I/O. When no
token is configured (demo mode), neither path makes a startup request.

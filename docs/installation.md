# Installation

## Requirements

- .NET 10.0 or newer
- C# latest stable language version

The SDK uses the platform `HttpClient` abstraction and `System.Text.Json`. Applications
provide the `HttpClient`; the SDK does not create or dispose one.

## NuGet

```powershell
dotnet add package MarketDataApp
```

Or add the package reference to a project file:

```xml
<PackageReference Include="MarketDataApp" Version="1.0.0" />
```

Check the [NuGet package](https://www.nuget.org/packages/MarketDataApp) for the current
published version.

## First request

```csharp
using MarketDataApp;

using var httpClient = new HttpClient();
// CreateAsync validates the token and seeds the rate-limit snapshot at startup.
// Use `new MarketDataClient(httpClient, options)` to skip startup validation.
var client = await MarketDataClient.CreateAsync(
    httpClient,
    new MarketDataClientOptions { ApiToken = "your-api-token" });

var response = await client.Stocks.GetQuoteAsync(
    "AAPL",
    cancellationToken: CancellationToken.None);

foreach (var quote in response.Values)
{
    Console.WriteLine($"{quote.Symbol}: {quote.Last}");
}
```

Do not replace `await` with `.Result` or `.Wait()`. See [client lifetime and DI](client.md)
for ASP.NET Core registration.

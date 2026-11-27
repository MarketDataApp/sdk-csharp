# Delete Token (C# SDK)

Delete the authenticated account's current API token so it can no longer authenticate requests. Requires a token.

## Making Requests

Use `DeleteTokenAsync` on the `Utilities` resource.

```csharp
Task<UtilitiesDeleteTokenResponse> DeleteTokenAsync(CancellationToken cancellationToken = default)
```

### Returns

`UtilitiesDeleteTokenResponse` wrapping a `bool`, `true` once the token is deleted.

> [!CAUTION]
> **[Irreversible]**
>
> Once this call succeeds, the token used to make it can no longer authenticate any request — including a retry of this same call. Generate a replacement token from the [Market Data Dashboard](https://www.marketdata.app/dashboard/) before calling this, not after.

## Examples

```csharp
using MarketDataApp;

using var client = await MarketDataClient.CreateAsync();

var deleted = (await client.Utilities.DeleteTokenAsync()).Values;
Console.WriteLine(deleted ? "Token deleted." : "Token was not deleted.");
```

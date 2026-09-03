# Options (C# SDK)

The C#/.NET SDK from Market Data provides methods to streamline your use of the Options endpoints. These methods provide a typed interface over the underlying HTTP requests and responses; every method is asynchronous and accepts an optional `CancellationToken`.

Reach the resource through `client.Options`. For CSV output, call the paired `*CsvAsync` method of the same name (for example `GetChainCsvAsync`).

## Options Endpoints

- [Option Chain (C# SDK)](./chain.md) — Retrieve a complete or filtered option chain with the C# SDK. Every contract returns a full quote with greeks, implied volatility and open interest.
- [Expirations (C# SDK)](./expirations.md)
- [Option Quotes (C# SDK)](./quotes.md)
- [Lookup (C# SDK)](./lookup.md) — Turn a human-readable option description into a well-formed OCC option symbol with the C# SDK and its OptionsLookupRequest type.

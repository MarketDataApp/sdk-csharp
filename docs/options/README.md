# Options

The C#/.NET SDK from Market Data provides methods to streamline your use of the Options endpoints. These methods provide a typed interface over the underlying HTTP requests and responses; every method is asynchronous and accepts an optional `CancellationToken`.

Reach the resource through `client.Options`. For CSV output, call the paired `*CsvAsync` method of the same name (for example `GetChainCsvAsync`).

## Options Endpoints

- [Chain](./chain.md)
- [Expirations](./expirations.md)
- [Quotes](./quotes.md)
- [Lookup](./lookup.md)

# Markets

The C#/.NET SDK from Market Data provides methods to streamline your use of the Markets endpoints. These methods provide a typed interface over the underlying HTTP requests and responses; every method is asynchronous and accepts an optional `CancellationToken`.

Reach the resource through `client.Markets`. For CSV output, call the paired `*CsvAsync` method of the same name (for example `GetStatusCsvAsync`).

## Markets Endpoints

- [Status](./status.md)

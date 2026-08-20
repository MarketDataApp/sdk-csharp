# Stocks

The C#/.NET SDK from Market Data provides methods to streamline your use of the Stocks endpoints. These methods provide a typed interface over the underlying HTTP requests and responses; every method is asynchronous and accepts an optional `CancellationToken`.

Reach the resource through `client.Stocks`. For CSV output, call the paired `*CsvAsync` method of the same name (for example `GetQuotesCsvAsync`).

## Stocks Endpoints

- [Candles](./candles.md)
- [Quotes](./quotes.md)
- [Prices](./prices.md)
- [Earnings](./earnings.md)
- [News](./news.md)

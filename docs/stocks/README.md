# Stocks (C# SDK)

The C#/.NET SDK from Market Data provides methods to streamline your use of the Stocks endpoints. These methods provide a typed interface over the underlying HTTP requests and responses; every method is asynchronous and accepts an optional `CancellationToken`.

Reach the resource through `client.Stocks`. For CSV output, call the paired `*CsvAsync` method of the same name (for example `GetQuotesCsvAsync`).

## Stocks Endpoints

- [Stock Candles (C# SDK)](./candles.md) — Retrieve historical open, high, low, close and volume candles for a stock with the C# SDK StockCandlesRequest and its resolution options.
- [Stock Quotes (C# SDK)](./quotes.md) — Retrieve real-time bid, ask, mid, last and volume for one or more stock symbols with the C# SDK, in single or multi-symbol form.
- [Prices (C# SDK)](./prices.md) — Retrieve the latest mid price and change for one or more stock symbols with the C# SDK, a lighter payload than a full quote.
- [Earnings (C# SDK)](./earnings.md)
- [News (C# SDK)](./news.md) — Retrieve news articles for a stock symbol with the C# SDK, using StockNewsRequest to set the symbol and the date window you want.

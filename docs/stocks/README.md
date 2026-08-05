# Stocks

Use `client.Stocks` for asynchronous stock endpoints. Each endpoint has a simple scalar
overload for common calls and a request-object overload for advanced filters. Both accept
optional `MarketDataRequestOptions` and a `CancellationToken`.

| Operation | Simple call | Advanced request |
|---|---|---|
| Single quote | `GetQuoteAsync("AAPL")` | `StockQuoteRequest` |
| Multi-symbol quotes | `GetQuotesAsync(["AAPL", "MSFT"])` | `StockQuotesRequest` |
| Multi-symbol prices | `GetPricesAsync(["AAPL", "MSFT"])` | `StockPricesRequest` |
| Single price | `GetPriceAsync("AAPL")` | `StockPriceRequest` |
| Candles | `GetCandlesAsync(StockResolution.Daily, "AAPL")` | `StockCandlesRequest` |
| News | `GetNewsAsync("AAPL")` | `StockNewsRequest` |
| Earnings | `GetEarningsAsync("AAPL")` | `StockEarningsRequest` |

Each operation has a corresponding `Get*CsvAsync` method.

```csharp
var quote = await client.Stocks.GetQuoteAsync("AAPL");

// Response wrappers and data records have concise ToString() summaries.
Console.WriteLine(quote);           // StockQuotesResponse: 1 item, HTTP 200
Console.WriteLine(quote.Values[0]); // AAPL mid=150.25 last=150.10

var prices = await client.Stocks.GetPricesAsync(["AAPL", "MSFT"]);

var response = await client.Stocks.GetCandlesAsync(
    StockResolution.Daily,
    "AAPL",
    countback: 30,
    cancellationToken: cancellationToken);

// Use a request object when several optional filters are needed.
var filtered = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest(StockResolution.Daily, "AAPL")
    {
        Countback = 30
    },
    cancellationToken: cancellationToken);

if (!response.IsNoData)
{
    foreach (var candle in response.Values)
    {
        Console.WriteLine($"{candle.Time:yyyy-MM-dd}: {candle.Close}");
    }
}
```

`StockResolution` includes `Daily`, `Weekly`, `Monthly`, `Yearly`, and factories such
as `Minutes(5)` and `Hours(1)`. Long intraday ranges are automatically chunked and
merged; inspect `IsComposite` and `Parts`. Bulk stock candles remain deferred because
the live schema has an inconsistent path definition.

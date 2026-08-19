# News

Retrieve news articles for a stock symbol.

## Making Requests

Use `GetNewsAsync` on the `Stocks` resource.

```csharp
Task<StockNewsResponse> GetNewsAsync(
    string symbol,
    DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null,
    MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default)
Task<StockNewsResponse> GetNewsAsync(StockNewsRequest request, ...)
```

### StockNewsRequest

```csharp
new StockNewsRequest(string symbol)
{
    Date = DateOnly,        // a single date
    From = DateOnly,        // window start
    To = DateOnly,          // window end
    Countback = int         // N most recent articles
}
```

The same date-window rules as [candles](./candles.md) apply and are validated before any HTTP call.

#### Returns

`StockNewsResponse` wrapping `IReadOnlyList<StockNewsArticle>`:

```csharp
public record StockNewsArticle(
    string Symbol,
    string Headline,
    string Content,
    string Source,
    DateTimeOffset PublicationDate);   // America/New_York
```

## Examples

```csharp
using MarketDataApp;

using var client = await MarketDataClient.CreateAsync();

var news = await client.Stocks.GetNewsAsync("AAPL", countback: 5);
foreach (var article in news.Values)
{
    Console.WriteLine($"[{article.PublicationDate:yyyy-MM-dd}] {article.Source}: {article.Headline}");
}
```

For CSV output, call `client.Stocks.GetNewsCsvAsync(...)` and read `.Csv`. See [Settings](../settings.md#csv-output).

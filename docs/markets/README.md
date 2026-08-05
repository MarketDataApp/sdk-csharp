# Markets

Use `client.Markets` to retrieve open/closed status for an exchange country and date
window. Scalar parameters are convenient for common queries; use `MarketStatusRequest`
for a reusable or advanced request.

| Operation | Simple call | Advanced request |
|---|---|---|
| Typed status | `GetStatusAsync(country: "US", countback: 5)` | `MarketStatusRequest` |
| CSV status | `GetStatusCsvAsync(country: "US", countback: 5)` | `MarketStatusRequest` |

```csharp
var recent = await client.Markets.GetStatusAsync(country: "US", countback: 5);

// Use a request object when passing a complete set of filters.
var response = await client.Markets.GetStatusAsync(
    new MarketStatusRequest
    {
        Country = "US",
        Countback = 5
    },
    cancellationToken: cancellationToken);

// Response wrappers and data records have concise ToString() summaries.
Console.WriteLine(response);           // MarketStatusResponse: 5 items, HTTP 200
Console.WriteLine(response.Values[0]); // 2025-01-10 open

foreach (var day in response.Values)
{
    Console.WriteLine($"{day.Date:yyyy-MM-dd}: {day.Status}");
}
```

`Country` is a two-letter ISO 3166 code and defaults to `US`. Date windows use
`Date`, `From`/`To`, or `Countback` according to the request validation rules.

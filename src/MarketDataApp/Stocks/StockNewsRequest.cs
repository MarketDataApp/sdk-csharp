namespace MarketDataApp.Stocks;

/// <summary>
/// Parameters for <c>GET /v1/stocks/news/{symbol}/</c>.
/// </summary>
/// <remarks>
/// A <c>columns</c> projection (via <see cref="MarketDataRequestOptions.Columns"/>) is honored
/// on the typed path like every other endpoint. Because <see cref="StockNewsArticle"/> fields
/// are non-nullable, a projected subset must still include the article columns
/// (<c>symbol</c>, <c>headline</c>, <c>content</c>, <c>source</c>, <c>publicationDate</c>);
/// the optional <c>updated</c> scalar may be omitted.
/// </remarks>
public record StockNewsRequest
{
    /// <summary>Ticker symbol.</summary>
    public string Symbol { get; init; }

    /// <summary>Return articles published on a single date only.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Start date (inclusive) of the publication window.</summary>
    public DateOnly? From { get; init; }

    /// <summary>End date (inclusive) of the publication window.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Number of articles to return, counting back from <see cref="To"/> (or today).</summary>
    public int? Countback { get; init; }

    /// <summary>Initializes the request with the required <paramref name="symbol"/>.</summary>
    public StockNewsRequest(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        Symbol = symbol;
    }
}

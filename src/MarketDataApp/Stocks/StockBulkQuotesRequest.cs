namespace MarketDataApp.Stocks;

/// <summary>Parameters for <c>GET /v1/stocks/bulkquotes/</c>.</summary>
public sealed record StockBulkQuotesRequest
{
    /// <summary>Ticker symbols to quote.</summary>
    public IReadOnlyList<string> Symbols { get; init; }

    /// <summary>Include extended-hours quote data.</summary>
    public bool? Extended { get; init; }

    /// <summary>
    /// Request a whole-market snapshot. When <c>true</c> the API returns quotes for every
    /// available ticker rather than only the supplied <see cref="Symbols"/>.
    /// </summary>
    public bool? Snapshot { get; init; }

    /// <summary>Initializes a request with one or more symbols.</summary>
    public StockBulkQuotesRequest(params string[] symbols) : this((IEnumerable<string>)symbols) { }

    /// <summary>Initializes a request from a symbol sequence.</summary>
    public StockBulkQuotesRequest(IEnumerable<string> symbols)
    {
        var values = (symbols ?? throw new ArgumentNullException(nameof(symbols))).ToList();
        if (values.Count == 0)
        {
            throw new ArgumentException("At least one symbol is required.", nameof(symbols));
        }

        if (values.Exists(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("All symbols must be non-empty strings.", nameof(symbols));
        }

        Symbols = values.AsReadOnly();
    }
}

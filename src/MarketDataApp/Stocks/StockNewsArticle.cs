namespace MarketDataApp.Stocks;

/// <summary>
/// A single news article for a stock. All fields are non-nullable — the news endpoint
/// always returns the full article shape. Use the CSV facet if you need a partial projection.
/// </summary>
/// <param name="Symbol">Ticker symbol the article is about.</param>
/// <param name="Headline">Article headline.</param>
/// <param name="Content">Full article body text.</param>
/// <param name="Source">Source URL of the article.</param>
/// <param name="PublicationDate">Date and time the article was published.</param>
public record StockNewsArticle(
    string Symbol,
    string Headline,
    string Content,
    string Source,
    DateTimeOffset PublicationDate)
{
    /// <summary>A concise one-line summary, e.g. <c>AAPL: Apple unveils... (2024-01-02)</c>.</summary>
    public override string ToString() =>
        $"{Symbol}: {ResponseText.Truncate(Headline)} ({ResponseText.D(PublicationDate)})";
}

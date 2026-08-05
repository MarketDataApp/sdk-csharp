namespace MarketDataApp.Stocks;

/// <summary>
/// A real-time stock quote row. All fields are nullable: the <c>columns</c> parameter can
/// project any field away, and the backend maps NaN to <c>null</c> for closed or illiquid markets.
/// </summary>
/// <remarks>
/// <para>OHLC fields (<see cref="Open"/> / <see cref="High"/> / <see cref="Low"/> / <see cref="Close"/>)
/// are only populated when the request opts in via <c>Candle = true</c>.</para>
/// <para>52-week extremes are only populated when the request opts in via <c>Week52 = true</c>.</para>
/// </remarks>
/// <param name="Symbol">Ticker symbol.</param>
/// <param name="Ask">Best ask price.</param>
/// <param name="AskSize">Best ask size (shares).</param>
/// <param name="Bid">Best bid price.</param>
/// <param name="BidSize">Best bid size (shares).</param>
/// <param name="Mid">Midpoint of the NBBO.</param>
/// <param name="Last">Last traded price.</param>
/// <param name="Change">Absolute price change vs the prior close.</param>
/// <param name="ChangePct">Fractional price change vs the prior close (wire field: <c>changepct</c>).</param>
/// <param name="Volume">Cumulative session volume.</param>
/// <param name="Updated">Quote timestamp (America/New_York).</param>
/// <param name="Open">Session open price (opt-in via <c>Candle = true</c>; wire field: <c>o</c>).</param>
/// <param name="High">Session high price (opt-in via <c>Candle = true</c>; wire field: <c>h</c>).</param>
/// <param name="Low">Session low price (opt-in via <c>Candle = true</c>; wire field: <c>l</c>).</param>
/// <param name="Close">Session close price (opt-in via <c>Candle = true</c>; wire field: <c>c</c>).</param>
/// <param name="Week52High">52-week high (opt-in via <c>Week52 = true</c>; wire field: <c>52weekHigh</c>).</param>
/// <param name="Week52Low">52-week low (opt-in via <c>Week52 = true</c>; wire field: <c>52weekLow</c>).</param>
public record StockQuote(
    string? Symbol,
    decimal? Ask,
    long? AskSize,
    decimal? Bid,
    long? BidSize,
    decimal? Mid,
    decimal? Last,
    decimal? Change,
    double? ChangePct,
    long? Volume,
    DateTimeOffset? Updated,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    decimal? Week52High,
    decimal? Week52Low)
{
    /// <summary>A concise one-line summary, e.g. <c>AAPL mid=150.25 last=150.10</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.F(Symbol)} mid={ResponseText.F(Mid)} last={ResponseText.F(Last)}";
}

namespace MarketDataApp.Funds;

/// <summary>
/// A single OHLC candle for a mutual fund or ETF. Volume is absent — funds report NAV
/// rather than traded volume.
/// All fields are nullable because the <c>columns</c> parameter can project any field away.
/// </summary>
/// <param name="Time">Candle date (wire field: <c>t</c>).</param>
/// <param name="Open">Opening NAV (wire field: <c>o</c>).</param>
/// <param name="High">High NAV (wire field: <c>h</c>).</param>
/// <param name="Low">Low NAV (wire field: <c>l</c>).</param>
/// <param name="Close">Closing NAV (wire field: <c>c</c>).</param>
public record FundCandle(
    DateTimeOffset? Time,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close)
{
    /// <summary>A concise one-line summary, e.g. <c>2024-01-02 O=450.10 H=452.00 L=449.50 C=451.25</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.D(Time)} O={ResponseText.F(Open)} H={ResponseText.F(High)} L={ResponseText.F(Low)} C={ResponseText.F(Close)}";
}

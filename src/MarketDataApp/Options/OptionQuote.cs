namespace MarketDataApp.Options;

/// <summary>
/// A single option contract quote row, used in both individual quotes
/// (<c>options.quote</c>) and chain responses (<c>options.chain</c>).
/// All fields are nullable — the <c>columns</c> parameter can project any field away,
/// and feeds may not provide all greeks for every contract.
/// </summary>
/// <param name="OptionSymbol">OCC option symbol (e.g. <c>AAPL250117C00150000</c>).</param>
/// <param name="Underlying">Underlying ticker symbol.</param>
/// <param name="Expiration">Expiration date and time.</param>
/// <param name="Side">Contract side: <c>"call"</c> or <c>"put"</c>.</param>
/// <param name="Strike">Strike price.</param>
/// <param name="FirstTraded">Date the contract first traded.</param>
/// <param name="Dte">Days to expiration.</param>
/// <param name="Updated">Quote timestamp.</param>
/// <param name="Bid">Best bid price.</param>
/// <param name="BidSize">Best bid size (contracts).</param>
/// <param name="Mid">Midpoint of the bid/ask.</param>
/// <param name="Ask">Best ask price.</param>
/// <param name="AskSize">Best ask size (contracts).</param>
/// <param name="Last">Last traded price.</param>
/// <param name="OpenInterest">Open interest (contracts).</param>
/// <param name="Volume">Session volume (contracts).</param>
/// <param name="InTheMoney">Whether the contract is in the money.</param>
/// <param name="IntrinsicValue">Intrinsic value of the option.</param>
/// <param name="ExtrinsicValue">Extrinsic (time) value of the option.</param>
/// <param name="UnderlyingPrice">Underlying price used for greek calculations.</param>
/// <param name="Iv">Implied volatility.</param>
/// <param name="Delta">Delta greek.</param>
/// <param name="Gamma">Gamma greek.</param>
/// <param name="Theta">Theta greek.</param>
/// <param name="Vega">Vega greek.</param>
/// <param name="Rho">Rho greek (optional — some feeds do not provide it).</param>
public record OptionQuote(
    string? OptionSymbol,
    string? Underlying,
    DateTimeOffset? Expiration,
    string? Side,
    decimal? Strike,
    DateTimeOffset? FirstTraded,
    int? Dte,
    DateTimeOffset? Updated,
    decimal? Bid,
    long? BidSize,
    decimal? Mid,
    decimal? Ask,
    long? AskSize,
    decimal? Last,
    long? OpenInterest,
    long? Volume,
    bool? InTheMoney,
    decimal? IntrinsicValue,
    decimal? ExtrinsicValue,
    decimal? UnderlyingPrice,
    double? Iv,
    double? Delta,
    double? Gamma,
    double? Theta,
    double? Vega,
    double? Rho)
{
    /// <summary>A concise one-line summary, e.g. <c>AAPL250117C00150000 mid=5.25 last=5.20</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.F(OptionSymbol)} mid={ResponseText.F(Mid)} last={ResponseText.F(Last)}";

    /// <summary>Returns the set of greeks that have non-null values on this quote.</summary>
    public IReadOnlySet<Greek> PresentGreeks
    {
        get
        {
            var greeks = new HashSet<Greek>();
            if (Delta.HasValue) greeks.Add(Greek.Delta);
            if (Gamma.HasValue) greeks.Add(Greek.Gamma);
            if (Theta.HasValue) greeks.Add(Greek.Theta);
            if (Vega.HasValue) greeks.Add(Greek.Vega);
            if (Rho.HasValue) greeks.Add(Greek.Rho);
            return greeks;
        }
    }

    /// <summary>Returns the value of <paramref name="greek"/>, or <c>null</c> if not present.</summary>
    public double? GetGreek(Greek greek) => greek switch
    {
        Greek.Delta => Delta,
        Greek.Gamma => Gamma,
        Greek.Theta => Theta,
        Greek.Vega => Vega,
        Greek.Rho => Rho,
        _ => throw new ArgumentOutOfRangeException(nameof(greek), greek, null),
    };
}

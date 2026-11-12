namespace MarketDataApp.Options;

/// <summary>
/// Parameters for <c>GET /v1/options/chain/{symbol}/</c>.
/// Returns the complete options chain for the underlying. All filter parameters are optional;
/// omitting them returns the full chain.
/// </summary>
public record OptionsChainRequest
{
    /// <summary>Underlying ticker symbol.</summary>
    public string Symbol { get; init; }

    /// <summary>
    /// Filter expirations by date, DTE (an integer, a comma-separated list, a range, or a
    /// comparison), a date range, or month/year.
    /// </summary>
    public ExpirationFilter? Expiration { get; init; }

    /// <summary>Include weekly expirations.</summary>
    public bool? Weekly { get; init; }

    /// <summary>Include monthly (standard) expirations.</summary>
    public bool? Monthly { get; init; }

    /// <summary>Include quarterly expirations.</summary>
    public bool? Quarterly { get; init; }

    /// <summary>Include AM-settled expirations.</summary>
    public bool? Am { get; init; }

    /// <summary>Include PM-settled expirations.</summary>
    public bool? Pm { get; init; }

    /// <summary>Include non-standard (mini, binary, etc.) expirations.</summary>
    public bool? NonStandard { get; init; }

    /// <summary>Filter by strike price (exact, range, or comparison).</summary>
    public StrikeFilter? Strike { get; init; }

    /// <summary>
    /// Filter to contracts whose delta is approximately this value.
    /// Applied after the chain is fetched.
    /// </summary>
    public double? Delta { get; init; }

    /// <summary>Maximum number of strikes to return above and below the underlying price.</summary>
    public int? StrikeLimit { get; init; }

    /// <summary>Filter to in-the-money, out-of-the-money, or all contracts.</summary>
    public StrikeRange? StrikeRangeFilter { get; init; }

    /// <summary>Minimum bid price filter.</summary>
    public decimal? MinBid { get; init; }

    /// <summary>Maximum bid price filter.</summary>
    public decimal? MaxBid { get; init; }

    /// <summary>Minimum ask price filter.</summary>
    public decimal? MinAsk { get; init; }

    /// <summary>Maximum ask price filter.</summary>
    public decimal? MaxAsk { get; init; }

    /// <summary>Maximum allowable bid/ask spread.</summary>
    public decimal? MaxBidAskSpread { get; init; }

    /// <summary>Maximum allowable bid/ask spread as a fraction of the mid price.</summary>
    public double? MaxBidAskSpreadPct { get; init; }

    /// <summary>Minimum open interest required.</summary>
    public long? MinOpenInterest { get; init; }

    /// <summary>Minimum session volume required.</summary>
    public long? MinVolume { get; init; }

    /// <summary>Restrict to calls or puts only.</summary>
    public OptionSide? Side { get; init; }

    /// <summary>Return the chain as-of this historical date rather than live.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Initializes the request with the required underlying <paramref name="symbol"/>.</summary>
    public OptionsChainRequest(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        Symbol = symbol;
    }
}

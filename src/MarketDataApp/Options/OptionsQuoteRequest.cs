namespace MarketDataApp.Options;

/// <summary>
/// Parameters for the single-contract option quote endpoint
/// <c>GET /v1/options/quotes/{optionSymbol}/</c>.
/// For multiple contracts use <see cref="OptionsQuotesRequest"/>.
/// </summary>
public record OptionsQuoteRequest
{
    /// <summary>OCC option symbol (e.g. <c>AAPL250117C00150000</c>).</summary>
    public string OptionSymbol { get; init; }

    /// <summary>Return the quote for a single historical date.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Start date (inclusive) of the historical quote series.</summary>
    public DateOnly? From { get; init; }

    /// <summary>End date (inclusive) of the historical quote series.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Initializes the request with the required <paramref name="optionSymbol"/>.</summary>
    public OptionsQuoteRequest(string optionSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionSymbol);
        OptionSymbol = optionSymbol;
    }
}

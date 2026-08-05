namespace MarketDataApp.Options;

/// <summary>
/// Parameters for multi-contract option quotes. One HTTP request is dispatched per symbol
/// concurrently and the results are returned as a dictionary keyed by option symbol.
/// For a single contract use <see cref="OptionsQuoteRequest"/>.
/// </summary>
public record OptionsQuotesRequest
{
    /// <summary>OCC option symbols to quote (at least one required).</summary>
    public IReadOnlyList<string> OptionSymbols { get; init; }

    /// <summary>Return quotes for a single historical date (applied to every symbol).</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Start date (inclusive) of the historical quote series (applied to every symbol).</summary>
    public DateOnly? From { get; init; }

    /// <summary>End date (inclusive) of the historical quote series (applied to every symbol).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Initializes the request with one or more OCC option symbols.</summary>
    public OptionsQuotesRequest(params string[] optionSymbols) : this((IEnumerable<string>)optionSymbols) { }

    /// <summary>Initializes the request from a sequence of OCC option symbols.</summary>
    public OptionsQuotesRequest(IEnumerable<string> optionSymbols)
    {
        var list = (optionSymbols ?? throw new ArgumentNullException(nameof(optionSymbols))).ToList();
        if (list.Count == 0) throw new ArgumentException("At least one option symbol is required.", nameof(optionSymbols));
        if (list.Exists(string.IsNullOrWhiteSpace))
            throw new ArgumentException("All option symbols must be non-empty strings.", nameof(optionSymbols));
        OptionSymbols = list.AsReadOnly();
    }
}

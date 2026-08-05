namespace MarketDataApp;

/// <summary>Response containing raw CSV text, returned by CSV-facet endpoints.</summary>
public sealed record CsvResponse : MarketDataResponse<string>
{
    /// <summary>The raw CSV text. Equivalent to <see cref="MarketDataResponse{T}.Values"/>.</summary>
    public string Csv => Values;

    /// <inheritdoc />
    public override bool IsJson => false;

    /// <inheritdoc />
    public override bool IsCsv => true;
}

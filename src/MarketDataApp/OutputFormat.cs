namespace MarketDataApp;

/// <summary>Advisory hint for the preferred serialization of endpoint output.</summary>
/// <remarks>
/// In this SDK the effective output format is chosen by <em>which method the caller invokes</em>:
/// the typed endpoint methods return typed models decoded from JSON, while the paired
/// <c>*CsvAsync</c> methods return CSV. <see cref="OutputFormat"/> (from
/// <c>MARKETDATA_OUTPUT_FORMAT</c>) is therefore advisory / default-hinting only — configuring it
/// does not reroute a typed method to CSV or a CSV method to JSON.
/// </remarks>
public enum OutputFormat
{
    /// <summary>JSON output, decoded into typed models by the typed endpoint methods.</summary>
    Json,

    /// <summary>CSV output, produced by the <c>*CsvAsync</c> endpoint methods.</summary>
    Csv,
}

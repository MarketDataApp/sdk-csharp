namespace MarketDataApp;

/// <summary>Optional parameters shared by data endpoints.</summary>
public sealed record MarketDataRequestOptions
{
    /// <summary>Response date/time format.</summary>
    public DateFormat? DateFormat { get; init; }
    /// <summary>Requested data freshness mode.</summary>
    public Mode? Mode { get; init; }
    /// <summary>Maximum number of returned rows.</summary>
    public int? Limit { get; init; }
    /// <summary>Number of rows to skip.</summary>
    public int? Offset { get; init; }
    /// <summary>Requested response columns. Applied to both typed JSON and CSV requests.</summary>
    public IReadOnlyList<string>? Columns { get; init; }
    /// <summary>
    /// Whether CSV output includes a header row. Applies to CSV requests only; typed JSON
    /// responses are always decoded into typed models and are unaffected by this flag.
    /// </summary>
    public bool? Headers { get; init; }
    /// <summary>
    /// Whether CSV output uses human-readable field names and values. Applies to CSV /
    /// human-readable output only. Typed JSON responses always return typed models keyed by
    /// property name regardless of this flag (the array-keyed JSON decoder requires the machine
    /// field names), so <c>human</c> is never sent on the typed JSON request path.
    /// </summary>
    public bool? Human { get; init; }
}

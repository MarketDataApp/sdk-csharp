namespace MarketDataApp.Markets;

/// <summary>
/// Market open/closed status for a single date. Both fields are nullable — the API returns
/// <c>null</c> for dates outside the exchange's known calendar coverage.
/// </summary>
/// <param name="Date">Calendar date of this status entry.</param>
/// <param name="Status">
/// <c>"open"</c>, <c>"closed"</c>, or <c>null</c> when outside calendar coverage.
/// Use <see cref="IsOpen"/> and <see cref="IsClosed"/> rather than comparing this string directly.
/// </param>
public record MarketStatus(DateTimeOffset? Date, string? Status)
{
    /// <summary>A concise one-line summary, e.g. <c>2025-01-10 open</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.D(Date)} {ResponseText.F(Status)}";

    /// <summary>Whether the market is open on this date.</summary>
    public bool IsOpen => Status == "open";

    /// <summary>Whether the market is closed on this date.</summary>
    public bool IsClosed => Status == "closed";
}

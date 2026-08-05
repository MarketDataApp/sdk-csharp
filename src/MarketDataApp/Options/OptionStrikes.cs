namespace MarketDataApp.Options;

/// <summary>Available strike prices grouped by expiration date.</summary>
/// <param name="Updated">Timestamp when the strike data was last updated.</param>
/// <param name="ByExpiration">Strike prices keyed by expiration date.</param>
public sealed record OptionStrikes(
    DateTimeOffset? Updated,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<decimal>> ByExpiration)
{
    /// <summary>A concise one-line summary, e.g. <c>3 expirations (updated 2024-01-02)</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.F(ByExpiration.Count)} expirations (updated {ResponseText.D(Updated)})";
}

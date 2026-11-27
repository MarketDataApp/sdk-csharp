namespace MarketDataApp;

/// <summary>US/Eastern time, the zone in which the API reads and writes calendar dates.</summary>
internal static class EasternTime
{
    private static readonly Lazy<TimeZoneInfo> LazyZone = new(ResolveZone);

    /// <summary>Gets the US/Eastern time zone.</summary>
    public static TimeZoneInfo Zone => LazyZone.Value;

    /// <summary>Returns the current calendar day in US/Eastern.</summary>
    /// <param name="timeProvider">The clock to read.</param>
    /// <returns>Today's date in US/Eastern.</returns>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime);

    /// <summary>Finds the US/Eastern zone by its IANA id, falling back to its Windows id.</summary>
    /// <returns>The US/Eastern time zone.</returns>
    /// <exception cref="TimeZoneNotFoundException">The system knows neither id.</exception>
    // Excluded: the "Eastern Standard Time" fallback is only reached on platforms that lack the
    // IANA "America/New_York" id (Windows). On Linux/macOS/CI — where the tests run — the primary
    // lookup always succeeds, so the catch/fallback branch is unreachable there.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
}

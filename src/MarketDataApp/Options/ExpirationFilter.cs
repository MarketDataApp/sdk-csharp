namespace MarketDataApp.Options;

/// <summary>
/// Discriminated union for filtering options chain expirations. Construct via the nested
/// record types (<see cref="OnDate"/>, <see cref="Dte"/>, etc.) or the static factory methods.
/// </summary>
/// <remarks>
/// Pattern-match on this type to handle each variant:
/// <code>
/// filter switch
/// {
///     ExpirationFilter.OnDate { Date: var d } => ...,
///     ExpirationFilter.Dte { Days: var days } => ...,
///     ExpirationFilter.DteList { Days: var days } => ...,
///     ExpirationFilter.DteRange { Min: var min, Max: var max } => ...,
///     ExpirationFilter.DteComparison { Op: var op, Days: var days } => ...,
///     ExpirationFilter.Between { From: var f, To: var t } => ...,
///     ExpirationFilter.MonthYear { Year: var y, Month: var m } => ...,
///     ExpirationFilter.All => ...,
/// }
/// </code>
/// </remarks>
public abstract record ExpirationFilter
{
    // Prevents subclassing outside this assembly.
    internal ExpirationFilter() { }

    /// <summary>Expirations on a specific calendar date.</summary>
    public sealed record OnDate(DateOnly Date) : ExpirationFilter;

    /// <summary>Expirations with approximately <see cref="Days"/> days to expiration.</summary>
    public sealed record Dte(int Days) : ExpirationFilter;

    /// <summary>
    /// The closest expiration to each days-to-expiry value, deduplicated by the API.
    /// </summary>
    public sealed record DteList : ExpirationFilter
    {
        /// <summary>The days-to-expiry values to select; each between 0 and 36500.</summary>
        public IReadOnlyList<int> Days { get; }

        /// <summary>Initializes the filter with the days-to-expiry values to select.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="days"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="days"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A value in <paramref name="days"/> is outside [0, 36500].</exception>
        public DteList(IEnumerable<int> days)
        {
            var list = (days ?? throw new ArgumentNullException(nameof(days))).ToList();
            if (list.Count == 0) throw new ArgumentException("At least one day count is required.", nameof(days));
            foreach (var day in list) ValidateDteDay(day, nameof(days));
            Days = list.AsReadOnly();
        }
    }

    /// <summary>Expirations whose days-to-expiry falls within a closed, inclusive range.</summary>
    public sealed record DteRange : ExpirationFilter
    {
        /// <summary>The minimum days-to-expiry, inclusive; between 0 and 36500.</summary>
        public int Min { get; }

        /// <summary>The maximum days-to-expiry, inclusive; between 0 and 36500.</summary>
        public int Max { get; }

        /// <summary>Initializes the filter with the inclusive days-to-expiry bounds.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> or <paramref name="max"/> is outside [0, 36500].</exception>
        /// <exception cref="ArgumentException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
        public DteRange(int min, int max)
        {
            ValidateDteDay(min, nameof(min));
            ValidateDteDay(max, nameof(max));
            if (min > max) throw new ArgumentException($"'{nameof(min)}' must be less than or equal to '{nameof(max)}'.");
            Min = min;
            Max = max;
        }
    }

    /// <summary>Expirations whose days-to-expiry satisfies a comparison against a threshold.</summary>
    public sealed record DteComparison : ExpirationFilter
    {
        /// <summary>The comparison operator, using the same syntax as <see cref="StrikeFilter.Comparison"/>.</summary>
        public StrikeFilter.ComparisonOperator Op { get; }

        /// <summary>The days-to-expiry threshold; between 0 and 36500.</summary>
        public int Days { get; }

        /// <summary>Initializes the filter with the comparison operator and days-to-expiry threshold.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is outside [0, 36500].</exception>
        public DteComparison(StrikeFilter.ComparisonOperator op, int days)
        {
            ValidateDteDay(days, nameof(days));
            Op = op;
            Days = days;
        }
    }

    /// <summary>Expirations falling within a date range (inclusive on both ends).</summary>
    public sealed record Between(DateOnly From, DateOnly To) : ExpirationFilter;

    /// <summary>Expirations in a specific calendar month.</summary>
    public sealed record MonthYear(int Year, int Month) : ExpirationFilter;

    /// <summary>All available expirations — no date filter applied.</summary>
    public sealed record All : ExpirationFilter;

    // ── Static factory helpers ──────────────────────────────────────────────

    /// <inheritdoc cref="OnDate"/>
    public static ExpirationFilter ForDate(DateOnly date) => new OnDate(date);

    /// <inheritdoc cref="Dte"/>
    public static ExpirationFilter ForDte(int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Days to expiration must be non-negative.");
        return new Dte(days);
    }

    /// <inheritdoc cref="DteList"/>
    public static ExpirationFilter ForDteList(IEnumerable<int> days) => new DteList(days);

    /// <inheritdoc cref="DteRange"/>
    public static ExpirationFilter ForDteRange(int min, int max) => new DteRange(min, max);

    /// <inheritdoc cref="DteComparison"/>
    public static ExpirationFilter ForDteComparison(StrikeFilter.ComparisonOperator op, int days) =>
        new DteComparison(op, days);

    /// <inheritdoc cref="Between"/>
    public static ExpirationFilter ForRange(DateOnly from, DateOnly to)
    {
        if (from > to) throw new ArgumentException($"'{nameof(from)}' must be on or before '{nameof(to)}'.");
        return new Between(from, to);
    }

    /// <inheritdoc cref="MonthYear"/>
    public static ExpirationFilter ForMonthYear(int year, int month)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        return new MonthYear(year, month);
    }

    /// <inheritdoc cref="All"/>
    public static ExpirationFilter AllDates() => new All();

    private static void ValidateDteDay(int days, string paramName)
    {
        if (days is < 0 or > 36500)
            throw new ArgumentOutOfRangeException(paramName, days, "Days to expiry must be between 0 and 36500.");
    }
}

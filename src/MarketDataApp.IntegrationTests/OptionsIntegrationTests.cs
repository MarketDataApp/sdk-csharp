using MarketDataApp.Options;

namespace MarketDataApp.IntegrationTests;

public sealed class OptionsIntegrationTests : IntegrationTestBase
{
    /// <summary>
    /// Verifies that live expirations are returned and that each one is midnight US/Eastern of
    /// its day, carrying the EST or EDT offset in force on that date.
    /// </summary>
    [IntegrationFact]
    public async Task Expirations_ReturnExpectedShape()
    {
        var response = await Client.Options.GetExpirationsAsync(
            new OptionsExpirationsRequest("AAPL"));
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.All(response.Values, expiration =>
        {
            Assert.Equal(TimeSpan.Zero, expiration.TimeOfDay);
            Assert.Equal(eastern.GetUtcOffset(expiration.DateTime), expiration.Offset);
        });
    }

    [IntegrationFact]
    public async Task Chain_ReturnsExpectedShape()
    {
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Side = OptionSide.Call,
                StrikeLimit = 2
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
    }

    [IntegrationFact]
    public async Task Quote_ReturnsExpectedShape()
    {
        // Resolve a currently-tradeable contract from the chain, then quote it directly.
        var chain = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Side = OptionSide.Call,
                StrikeLimit = 1
            });
        AssertSuccess(chain.StatusCode);
        var optionSymbol = chain.Values[0].OptionSymbol;
        Assert.False(string.IsNullOrWhiteSpace(optionSymbol));

        var response = await Client.Options.GetQuoteAsync(new OptionsQuoteRequest(optionSymbol!));

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
    }

    /// <summary>
    /// A single <c>dte</c> value (the form the SDK has always sent as a plain integer)
    /// still limits the chain to contracts sharing one closest expiration.
    /// </summary>
    [IntegrationFact]
    public async Task Chain_DteSingleValue_SelectsOneClosestExpiration()
    {
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL") { Expiration = ExpirationFilter.ForDte(30) });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.Single(DistinctExpirations(response.Values));
    }

    /// <summary>
    /// A comma-separated <c>dte</c> list selects, for each listed value, the expiration(s)
    /// among those the underlying actually offers that are closest to it (ties included).
    /// Since AAPL's offered expirations closest to 15, 30, and 45 days are not all the same
    /// one, a single collapsed expiration cannot satisfy this.
    /// </summary>
    /// <remarks>
    /// Compared by calendar date, not by instant: <c>GetExpirationsAsync</c> reports each
    /// expiration at midnight Eastern, while the chain's <see cref="OptionQuote.Expiration"/>
    /// carries the contract's settlement time of day (e.g. 16:00 Eastern for a standard
    /// contract), so the two would never be equal as full timestamps for the same date.
    /// </remarks>
    [IntegrationFact]
    public async Task Chain_DteCommaSeparatedList_SelectsClosestExpirationPerValue()
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, eastern).DateTime);

        var expirations = await Client.Options.GetExpirationsAsync(new OptionsExpirationsRequest("AAPL"));
        AssertSuccess(expirations.StatusCode);
        Assert.NotEmpty(expirations.Values);
        var expirationDates = expirations.Values.Select(expiration => DateOnly.FromDateTime(expiration.DateTime)).ToList();

        int[] targets = [15, 30, 45];
        var candidatesByTarget = targets
            .Select(target => ClosestExpirations(expirationDates, today, target))
            .ToList();
        var allCandidates = candidatesByTarget.SelectMany(candidates => candidates).ToHashSet();

        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteExpression("15,30,45")
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        var distinctExpirationDates = response.Values
            .Select(contract => DateOnly.FromDateTime(contract.Expiration!.Value.DateTime))
            .Distinct()
            .ToList();

        // Every returned expiration is a nearest candidate for at least one requested value.
        Assert.All(distinctExpirationDates, date => Assert.Contains(date, allCandidates));

        // Each requested value is represented by at least one of its nearest candidates.
        Assert.All(candidatesByTarget, candidates => Assert.Contains(distinctExpirationDates, candidates.Contains));

        // The per-value candidate sets are not all identical, so a server that collapsed the
        // list to a single value cannot satisfy this.
        Assert.True(distinctExpirationDates.Count > 1);
    }

    /// <summary>
    /// A closed <c>dte</c> range returns every expiration whose dte falls within the
    /// bounds, inclusive on both ends — more than the one expiration a single-value dte
    /// would select.
    /// </summary>
    [IntegrationFact]
    public async Task Chain_DteClosedRange_ReturnsOnlyExpirationsWithinBounds()
    {
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteExpression("0-45")
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.All(response.Values, contract => Assert.InRange(contract.Dte!.Value, 0, 45));
        Assert.True(DistinctExpirations(response.Values).Count > 1);
    }

    /// <summary>
    /// A <c>dte</c> comparison of <c>&gt;=</c> keeps the boundary value and everything
    /// above it — more than the one expiration a single-value dte would select.
    /// </summary>
    [IntegrationFact]
    public async Task Chain_DteGreaterThanOrEqual_KeepsBoundaryAndAbove()
    {
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteExpression(">=30")
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.All(response.Values, contract => Assert.True(contract.Dte >= 30));
        Assert.True(DistinctExpirations(response.Values).Count > 1);
    }

    /// <summary>
    /// A <c>dte</c> comparison of <c>&lt;</c> excludes the boundary value and keeps only
    /// what falls strictly below it — more than the one expiration a single-value dte
    /// would select.
    /// </summary>
    [IntegrationFact]
    public async Task Chain_DteLessThan_ExcludesBoundaryAndAbove()
    {
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteExpression("<45")
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.All(response.Values, contract => Assert.True(contract.Dte < 45));
        Assert.True(DistinctExpirations(response.Values).Count > 1);
    }

    private static List<DateTimeOffset?> DistinctExpirations(IReadOnlyList<OptionQuote> contracts) =>
        contracts.Select(contract => contract.Expiration).Distinct().ToList();

    /// <summary>
    /// Among the given <paramref name="expirationDates"/>, the one(s) whose days-to-expiry
    /// (from <paramref name="today"/>) are closest to <paramref name="targetDte"/>, ties
    /// included.
    /// </summary>
    private static List<DateOnly> ClosestExpirations(
        IReadOnlyList<DateOnly> expirationDates, DateOnly today, int targetDte)
    {
        var withDistance = expirationDates
            .Select(expiration => (
                expiration,
                distance: Math.Abs(expiration.DayNumber - today.DayNumber - targetDte)))
            .ToList();
        var minDistance = withDistance.Min(item => item.distance);
        return withDistance.Where(item => item.distance == minDistance).Select(item => item.expiration).ToList();
    }

    [IntegrationFact]
    public async Task Lookup_ResolvesOptionSymbol()
    {
        // Derive a currently-listed contract from the chain so the lookup input isn't
        // tied to a hard-coded expiration/strike that expires or delists over time.
        var chain = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL") { Side = OptionSide.Call, StrikeLimit = 1 });
        AssertSuccess(chain.StatusCode);
        var contract = chain.Values[0];
        Assert.NotNull(contract.Expiration);
        Assert.NotNull(contract.Strike);

        var human = FormattableString.Invariant(
            $"AAPL {contract.Expiration.Value:M/d/yyyy} {contract.Strike.Value:0.##} Call");
        var response = await Client.Options.GetLookupAsync(new OptionsLookupRequest(human));

        AssertSuccess(response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Values));
    }
}

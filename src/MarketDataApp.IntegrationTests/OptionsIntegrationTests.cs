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

    [IntegrationFact]
    public async Task Chain_DteSingleValue_ReturnsOneExpirationClosestToTarget()
    {
        // A single dte value narrows the whole chain to one expiration (the one closest to the
        // target); an unfiltered chain spans many distinct expirations, so this distinguishes.
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDte(45),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.Single(response.Values.Select(contract => contract.Expiration).Distinct());
    }

    [IntegrationFact]
    public async Task Chain_DteCommaList_ReturnsClosestExpirationPerValue()
    {
        // A comma-separated dte list selects the closest listed expiration for each value,
        // deduplicated. Compute the candidates independently from a baseline expirations call
        // (the listed expiration(s) closest to each of 7, 30, 60, ties included) so the test
        // fails if the server honoured only one of the three values instead of the whole list.
        var today = TodayEastern();
        var expirations = await Client.Options.GetExpirationsAsync(new OptionsExpirationsRequest("AAPL"));
        AssertSuccess(expirations.StatusCode);
        Assert.NotEmpty(expirations.Values);

        int[] targets = [7, 30, 60];
        var candidatesPerTarget = targets
            .Select(target => ClosestExpirations(expirations.Values, today, target))
            .ToList();
        var allCandidates = candidatesPerTarget.SelectMany(candidates => candidates).Distinct().ToList();

        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteList(targets),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        // The expirations endpoint returns midnight Eastern for the day; the chain endpoint's
        // per-contract expiration is the market-close moment (16:00 Eastern) of the same day.
        // Compare calendar dates, not instants.
        var returned = response.Values.Select(contract => EasternDate(contract.Expiration!.Value)).Distinct().ToList();

        // Every returned expiration must be a closest match for at least one requested value.
        Assert.All(returned, expiration => Assert.Contains(expiration, allCandidates));

        // Every requested value must be represented by at least one of its closest matches.
        Assert.All(candidatesPerTarget, candidates => Assert.Contains(candidates, returned.Contains));

        // When the three values resolve to different expirations, dedup cannot collapse them to one.
        if (allCandidates.Count > 1)
        {
            Assert.True(returned.Count > 1);
        }
    }

    [IntegrationFact]
    public async Task Chain_DteRange_ReturnsOnlyExpirationsWithinInclusiveBounds()
    {
        // A closed dte range keeps every expiration within the bounds, inclusive. Compute the
        // expected set independently from a baseline expirations call so the test fails if the
        // server read "0-45" as a single value instead of the whole range.
        var today = TodayEastern();
        var expirations = await Client.Options.GetExpirationsAsync(new OptionsExpirationsRequest("AAPL"));
        AssertSuccess(expirations.StatusCode);
        Assert.NotEmpty(expirations.Values);

        var expected = expirations.Values
            .Where(expiration => DteFromToday(expiration, today) is >= 0 and <= 45)
            .Select(EasternDate)
            .Distinct()
            .ToList();

        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteRange(0, 45),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        // The expirations endpoint returns midnight Eastern for the day; the chain endpoint's
        // per-contract expiration is the market-close moment (16:00 Eastern) of the same day.
        // Compare calendar dates, not instants.
        var returned = response.Values.Select(contract => EasternDate(contract.Expiration!.Value)).Distinct().ToList();

        Assert.Equal(expected.OrderBy(e => e), returned.OrderBy(e => e));
        Assert.True(returned.Count > 1);
    }

    [IntegrationFact]
    public async Task Chain_DteComparison_FiltersInclusivelyAndExclusively()
    {
        // >=30 keeps a row at exactly 30 dte; <45 excludes a row at exactly 45 dte. AAPL lists
        // near-dated (<30) and far-dated (>=45) contracts, so an unfiltered chain fails both.
        var atLeast = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteComparison(StrikeFilter.ComparisonOperator.Gte, 30),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });
        AssertSuccess(atLeast.StatusCode);
        Assert.NotEmpty(atLeast.Values);
        Assert.All(atLeast.Values, contract => Assert.True(contract.Dte >= 30));

        var lessThan = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteComparison(StrikeFilter.ComparisonOperator.Lt, 45),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });
        AssertSuccess(lessThan.StatusCode);
        Assert.NotEmpty(lessThan.Values);
        Assert.All(lessThan.Values, contract => Assert.True(contract.Dte < 45));
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

    // Today's calendar date in US/Eastern, the zone expirations and dte are both anchored to
    // (see Expirations_ReturnExpectedShape): comparing UTC "today" against an Eastern expiration
    // date would misclassify dte near the UTC/Eastern midnight boundary.
    private static DateOnly TodayEastern()
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, eastern).Date);
    }

    private static DateOnly EasternDate(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);

    private static int DteFromToday(DateTimeOffset expiration, DateOnly today) =>
        EasternDate(expiration).DayNumber - today.DayNumber;

    private static IReadOnlyList<DateOnly> ClosestExpirations(
        IReadOnlyList<DateTimeOffset> expirations, DateOnly today, int targetDays)
    {
        var byDistance = expirations
            .Select(expiration => (Date: EasternDate(expiration), Distance: Math.Abs(DteFromToday(expiration, today) - targetDays)))
            .ToList();
        var minDistance = byDistance.Min(candidate => candidate.Distance);
        return byDistance
            .Where(candidate => candidate.Distance == minDistance)
            .Select(candidate => candidate.Date)
            .Distinct()
            .ToList();
    }
}

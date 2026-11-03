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
        // deduplicated. Compute the candidates independently from a baseline chain call's own
        // Dte field (live response metadata, not a client-computed "today"), taken with the
        // widest legal dte range (0-36500) so every listed expiration comes back with its Dte.
        var baseline = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteRange(0, 36500),
                Side = OptionSide.Call,
                StrikeLimit = 1
            });
        AssertSuccess(baseline.StatusCode);
        Assert.NotEmpty(baseline.Values);
        var dteByExpiration = DteByExpiration(baseline.Values);

        int[] targets = [7, 30, 60];
        var candidatesPerTarget = targets
            .Select(target => ClosestExpirations(dteByExpiration, target))
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
        var returned = response.Values.Select(contract => EasternDate(contract.Expiration!.Value)).Distinct().ToList();

        // Every returned expiration must be a closest match for at least one requested value.
        Assert.All(returned, expiration => Assert.Contains(expiration, allCandidates));

        // Every requested value must be represented by at least one of its closest matches.
        Assert.All(candidatesPerTarget, candidates => Assert.Contains(candidates, returned.Contains));

        // Only require more than one distinct expiration when no date is common to all three
        // targets' candidates: a tie that legitimately resolves all three values to one shared
        // expiration must not make the test demand a second one.
        var commonToAllTargets = candidatesPerTarget.Aggregate((a, b) => a.Intersect(b).ToList());
        if (commonToAllTargets.Count == 0)
        {
            Assert.True(returned.Count > 1);
        }
    }

    [IntegrationFact]
    public async Task Chain_DteRange_ReturnsOnlyExpirationsWithinInclusiveBounds()
    {
        // A closed dte range keeps every expiration within the bounds, inclusive. Assert the
        // per-row bound from the response's own Dte field — a single live call, not two whose
        // server-side "now" could differ — and require more than one distinct expiration, so
        // the test fails if the server read "0-45" as a single value instead of the whole range.
        var response = await Client.Options.GetChainAsync(
            new OptionsChainRequest("AAPL")
            {
                Expiration = ExpirationFilter.ForDteRange(0, 45),
                Side = OptionSide.Call,
                StrikeLimit = 2
            });

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
        Assert.All(response.Values, contract => Assert.InRange(contract.Dte!.Value, 0, 45));

        var returned = response.Values.Select(contract => EasternDate(contract.Expiration!.Value)).Distinct().ToList();
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

    private static DateOnly EasternDate(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);

    // Each contract's Dte is computed live by the server (days from its own notion of "now" to
    // the contract's expiration); reading it off the baseline response, rather than computing
    // it from the client's wall clock, keeps the test from drifting near the UTC/Eastern
    // midnight boundary, where the runner's "today" can differ from the server's.
    private static IReadOnlyDictionary<DateOnly, int> DteByExpiration(IReadOnlyList<OptionQuote> contracts) =>
        contracts
            .Where(contract => contract.Expiration.HasValue && contract.Dte.HasValue)
            .GroupBy(contract => EasternDate(contract.Expiration!.Value))
            .ToDictionary(group => group.Key, group => group.First().Dte!.Value);

    private static IReadOnlyList<DateOnly> ClosestExpirations(
        IReadOnlyDictionary<DateOnly, int> dteByExpiration, int targetDays)
    {
        var byDistance = dteByExpiration
            .Select(pair => (Date: pair.Key, Distance: Math.Abs(pair.Value - targetDays)))
            .ToList();
        var minDistance = byDistance.Min(candidate => candidate.Distance);
        return byDistance
            .Where(candidate => candidate.Distance == minDistance)
            .Select(candidate => candidate.Date)
            .Distinct()
            .ToList();
    }
}

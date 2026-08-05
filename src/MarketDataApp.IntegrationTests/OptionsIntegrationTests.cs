using MarketDataApp.Options;

namespace MarketDataApp.IntegrationTests;

public sealed class OptionsIntegrationTests : IntegrationTestBase
{
    [IntegrationFact]
    public async Task Expirations_ReturnExpectedShape()
    {
        var response = await Client.Options.GetExpirationsAsync(
            new OptionsExpirationsRequest("AAPL"));

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values);
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
    public async Task Strikes_ReturnExpectedShape()
    {
        var response = await Client.Options.GetStrikesAsync(
            new OptionsStrikesRequest("AAPL"));

        AssertSuccess(response.StatusCode);
        Assert.NotEmpty(response.Values.ByExpiration);
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

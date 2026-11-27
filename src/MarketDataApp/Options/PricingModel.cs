namespace MarketDataApp.Options;

/// <summary>
/// Pricing model for <c>iv</c> and the greeks on a historical options request. Only valid
/// alongside a historical date (<c>date</c>, or <c>from</c>/<c>to</c> on the quotes endpoint);
/// omitting it lets the API apply its own broker-convention default per contract type.
/// </summary>
public enum PricingModel
{
    /// <summary>Black-Scholes-Merton. Prices every contract, American or European.</summary>
    Bsm,

    /// <summary>Bjerksund-Stensland 2002. Prices American contracts; European contracts keep <c>bsm</c>.</summary>
    Bjs,

    /// <summary>Cox-Ross-Rubinstein binomial tree. Prices American contracts; European contracts keep <c>bsm</c>.</summary>
    Crr,

    /// <summary>Barone-Adesi-Whaley. Prices American contracts; European contracts keep <c>bsm</c>.</summary>
    Baw,
}

internal static class PricingModelExtensions
{
    internal static string ToWireValue(this PricingModel model) => model switch
    {
        PricingModel.Bsm => "bsm",
        PricingModel.Bjs => "bjs",
        PricingModel.Crr => "crr",
        PricingModel.Baw => "baw",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, null),
    };
}

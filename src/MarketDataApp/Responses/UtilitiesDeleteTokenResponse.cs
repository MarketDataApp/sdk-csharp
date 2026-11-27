namespace MarketDataApp;

/// <summary>Response for <c>utilities.DeleteToken()</c> — confirmation that the token was deleted.</summary>
public sealed record UtilitiesDeleteTokenResponse : MarketDataResponse<bool>;

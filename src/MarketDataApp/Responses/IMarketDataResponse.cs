namespace MarketDataApp;

/// <summary>
/// Common contract implemented by every response object the SDK can hand back to a caller,
/// exposing which wire format the response was decoded from. Exactly one of
/// <see cref="IsJson"/>, <see cref="IsCsv"/>, or <see cref="IsHtml"/> is <c>true</c>.
/// </summary>
public interface IMarketDataResponse
{
    /// <summary>Whether this response was decoded from a JSON payload.</summary>
    bool IsJson { get; }

    /// <summary>Whether this response carries raw CSV text.</summary>
    bool IsCsv { get; }

    /// <summary>Whether this response carries raw HTML text.</summary>
    bool IsHtml { get; }
}

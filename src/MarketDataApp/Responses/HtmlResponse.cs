namespace MarketDataApp;

/// <summary>
/// Response containing raw HTML text, returned by HTML-facet endpoints.
/// The HTML facet is plumbed through the SDK but not currently exposed on any resource
/// because the API does not serve HTML for data endpoints. It will be enabled when the
/// server adds support.
/// </summary>
public sealed record HtmlResponse : MarketDataResponse<string>
{
    /// <summary>The raw HTML text. Equivalent to <see cref="MarketDataResponse{T}.Values"/>.</summary>
    public string Html => Values;

    /// <inheritdoc />
    public override bool IsJson => false;

    /// <inheritdoc />
    public override bool IsHtml => true;
}

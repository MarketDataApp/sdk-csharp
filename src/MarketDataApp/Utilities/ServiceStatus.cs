namespace MarketDataApp.Utilities;

/// <summary>
/// Operational status of a single Market Data API service endpoint.
/// All fields are non-null.
/// </summary>
/// <param name="Service">Service path (e.g. <c>"/v1/stocks/quotes/"</c>).</param>
/// <param name="Status">
/// Status string: <c>"online"</c> or <c>"offline"</c>. Intentionally stringly-typed
/// to remain forward-compatible with new status values.
/// </param>
/// <param name="Online">Server-supplied boolean indicating whether the service is operational.</param>
/// <param name="UptimePct30d">30-day uptime percentage in the range [0, 1].</param>
/// <param name="UptimePct90d">90-day uptime percentage in the range [0, 1].</param>
/// <param name="Updated">Timestamp of the status reading (America/New_York).</param>
public record ServiceStatus(
    string Service,
    string Status,
    bool Online,
    double UptimePct30d,
    double UptimePct90d,
    DateTimeOffset Updated)
{
    /// <summary>A concise one-line summary, e.g. <c>/v1/stocks/quotes/ online</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.F(Service)} {ResponseText.F(Status)}";
}

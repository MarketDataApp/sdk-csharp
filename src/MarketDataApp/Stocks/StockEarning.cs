namespace MarketDataApp.Stocks;

/// <summary>
/// One earnings row for a stock. Fields that the backend does not emit for future-quarter
/// or fundamentals-missing rows decode to <c>null</c>.
/// </summary>
/// <param name="Symbol">Ticker symbol.</param>
/// <param name="FiscalYear">Fiscal year of the earnings period.</param>
/// <param name="FiscalQuarter">Fiscal quarter (1–4).</param>
/// <param name="Date">End date of the fiscal period.</param>
/// <param name="ReportDate">Date the earnings were reported (or expected).</param>
/// <param name="ReportTime">Time of the earnings call: <c>"bmo"</c>, <c>"amc"</c>, or <c>"dmh"</c>.</param>
/// <param name="Currency">Reporting currency (e.g. <c>"USD"</c>).</param>
/// <param name="ReportedEps">Actual reported EPS (wire field: <c>reportedEPS</c>).</param>
/// <param name="EstimatedEps">Analyst consensus estimate for EPS (wire field: <c>estimatedEPS</c>).</param>
/// <param name="SurpriseEps">EPS surprise (reported minus estimated; wire field: <c>surpriseEPS</c>).</param>
/// <param name="SurpriseEpsPct">EPS surprise as a fraction of the estimate (wire field: <c>surpriseEPSpct</c>).</param>
/// <param name="Updated">Timestamp of the last data update.</param>
public record StockEarning(
    string? Symbol,
    int? FiscalYear,
    int? FiscalQuarter,
    DateTimeOffset? Date,
    DateTimeOffset? ReportDate,
    string? ReportTime,
    string? Currency,
    decimal? ReportedEps,
    decimal? EstimatedEps,
    decimal? SurpriseEps,
    double? SurpriseEpsPct,
    DateTimeOffset? Updated)
{
    /// <summary>A concise one-line summary, e.g. <c>AAPL FY2024 Q1 eps=1.50</c>.</summary>
    public override string ToString() =>
        $"{ResponseText.F(Symbol)} FY{ResponseText.F(FiscalYear)} Q{ResponseText.F(FiscalQuarter)} eps={ResponseText.F(ReportedEps)}";
}

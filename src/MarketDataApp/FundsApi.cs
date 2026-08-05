using MarketDataApp.Funds;

namespace MarketDataApp;

/// <summary>Asynchronous fund and ETF endpoints.</summary>
public sealed class FundsApi
{
    private readonly ApiClient _apiClient;

    internal FundsApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets OHLC candles for a fund or ETF symbol.</summary>
    public Task<FundCandlesResponse> GetCandlesAsync(FundResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesAsync(new FundCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<FundCandlesResponse> GetCandlesAsync(
        FundCandlesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        var path =
            $"funds/candles/{Uri.EscapeDataString(request.Resolution.WireValue)}/{Uri.EscapeDataString(request.Symbol)}";

        var response = await _apiClient.GetAsync(path, true, query, cancellationToken)
            .ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new FundCandle(
                    row.Timestamp("t"),
                    row.Decimal("o"),
                    row.Decimal("h"),
                    row.Decimal("l"),
                    row.Decimal("c")),
                "t", "o", "h", "l", "c"),
            Array.Empty<FundCandle>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<FundCandlesResponse, IReadOnlyList<FundCandle>>(response, values);
    }

    /// <summary>Gets CSV OHLC candles for a fund or ETF symbol.</summary>
    public Task<CsvResponse> GetCandlesCsvAsync(FundResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesCsvAsync(new FundCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetCandlesCsvAsync(
        FundCandlesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        var path =
            $"funds/candles/{Uri.EscapeDataString(request.Resolution.WireValue)}/{Uri.EscapeDataString(request.Symbol)}";
        var response = await _apiClient.GetAsync(path, true, query, cancellationToken)
            .ConfigureAwait(false);
        return JsonResponseParser.CreateCsvResponse(response);
    }

}

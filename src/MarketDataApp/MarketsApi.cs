using System.Text.Json;
using MarketDataApp.Markets;

namespace MarketDataApp;

/// <summary>Asynchronous market-calendar endpoints.</summary>
public sealed class MarketsApi
{
    private readonly ApiClient _apiClient;

    internal MarketsApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets exchange open/closed status for the requested dates.</summary>
    public Task<MarketStatusResponse> GetStatusAsync(string? country = null, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStatusAsync(new MarketStatusRequest { Country = country, Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<MarketStatusResponse> GetStatusAsync(
        MarketStatusRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.Add(query, "country", request.Country);
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);

        var response = await _apiClient.GetAsync("markets/status", true, query, cancellationToken)
            .ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new MarketStatus(row.Timestamp("date"), row.String("status")),
                "date", "status"),
            Array.Empty<MarketStatus>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<MarketStatusResponse, IReadOnlyList<MarketStatus>>(response, values);
    }

    /// <summary>Gets exchange open/closed status as CSV.</summary>
    public Task<CsvResponse> GetStatusCsvAsync(string? country = null, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStatusCsvAsync(new MarketStatusRequest { Country = country, Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetStatusCsvAsync(
        MarketStatusRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.Add(query, "country", request.Country);
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        var response = await _apiClient.GetAsync("markets/status", true, query, cancellationToken)
            .ConfigureAwait(false);
        return JsonResponseParser.CreateCsvResponse(response);
    }

    private static void ValidateRequest(MarketStatusRequest request)
    {
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        if (request.Country is { Length: not 2 })
        {
            throw new ArgumentException(
                "Country must be a two-letter ISO 3166 country code.",
                nameof(request));
        }
    }
}

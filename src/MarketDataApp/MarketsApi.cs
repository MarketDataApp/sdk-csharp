using System.Text.Json;
using MarketDataApp.Exceptions;
using MarketDataApp.Markets;

namespace MarketDataApp;

/// <summary>Asynchronous market-calendar endpoints.</summary>
public sealed class MarketsApi
{
    private readonly ApiClient _apiClient;

    internal MarketsApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets exchange open/closed status for the requested dates.</summary>
    /// <param name="country">Two-letter ISO 3166 country code; defaults to <c>US</c> server-side.</param>
    /// <param name="date">Single day to check; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of days counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One open/closed reading per day; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="country"/> is not a two-letter code, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Markets.GetStatusAsync("US");
    /// Console.WriteLine($"{response.Values[0].Date:yyyy-MM-dd}: {response.Values[0].Status}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/markets/status/"/>
    public Task<MarketStatusResponse> GetStatusAsync(string? country = null, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStatusAsync(new MarketStatusRequest { Country = country, Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets exchange open/closed status for the requested dates.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One open/closed reading per day; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request's <c>Country</c> is not a two-letter code, or its date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/markets/status/"/>
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

    /// <summary>Gets exchange open/closed status for the requested dates as CSV.</summary>
    /// <param name="country">Two-letter ISO 3166 country code; defaults to <c>US</c> server-side.</param>
    /// <param name="date">Single day to check; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of days counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentException"><paramref name="country"/> is not a two-letter code, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Markets.GetStatusCsvAsync("US");
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/markets/status/"/>
    public Task<CsvResponse> GetStatusCsvAsync(string? country = null, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStatusCsvAsync(new MarketStatusRequest { Country = country, Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets exchange open/closed status for the requested dates as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request's <c>Country</c> is not a two-letter code, or its date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/markets/status/"/>
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

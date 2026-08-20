using MarketDataApp.Exceptions;
using MarketDataApp.Funds;

namespace MarketDataApp;

/// <summary>Asynchronous fund and ETF endpoints.</summary>
public sealed class FundsApi
{
    private readonly ApiClient _apiClient;

    internal FundsApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets OHLC candles for a mutual fund or ETF symbol.</summary>
    /// <param name="resolution">Candle duration, e.g. <c>FundResolution.Daily</c>.</param>
    /// <param name="symbol">The fund or ETF ticker symbol.</param>
    /// <param name="date">Single trading day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of candles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The parsed candles; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Funds.GetCandlesAsync(FundResolution.Daily, "VFINX", countback: 5);
    /// Console.WriteLine(response.Values[0].Close);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/funds/candles/"/>
    public Task<FundCandlesResponse> GetCandlesAsync(FundResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesAsync(new FundCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets OHLC candles for a mutual fund or ETF symbol.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The parsed candles; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request's date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/funds/candles/"/>
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

    /// <summary>Gets OHLC candles for a mutual fund or ETF symbol as CSV.</summary>
    /// <param name="resolution">Candle duration, e.g. <c>FundResolution.Daily</c>.</param>
    /// <param name="symbol">The fund or ETF ticker symbol.</param>
    /// <param name="date">Single trading day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of candles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Funds.GetCandlesCsvAsync(FundResolution.Daily, "VFINX", countback: 5);
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/funds/candles/"/>
    public Task<CsvResponse> GetCandlesCsvAsync(FundResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesCsvAsync(new FundCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets OHLC candles for a mutual fund or ETF symbol as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request's date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/funds/candles/"/>
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

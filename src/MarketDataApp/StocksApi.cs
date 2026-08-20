using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using System.Text;
using System.Text.Json;

namespace MarketDataApp;

/// <summary>Asynchronous stock endpoints.</summary>
public sealed class StocksApi
{
    private readonly ApiClient _apiClient;

    internal StocksApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets a real-time or delayed quote for one stock symbol.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="extended">Include extended-hours (pre/post market) data.</param>
    /// <param name="candle">Include the current session's OHLC fields on the quote.</param>
    /// <param name="week52">Include the 52-week high and low on the quote.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The parsed quote; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetQuoteAsync("AAPL");
    /// Console.WriteLine(response.Values[0].Mid);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public Task<StockQuotesResponse> GetQuoteAsync(string symbol, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteAsync(new StockQuoteRequest(symbol) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Gets a real-time or delayed quote for one stock symbol.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The parsed quote; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public async Task<StockQuotesResponse> GetQuoteAsync(
        StockQuoteRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.Add(query, "extended", request.Extended?.ToString().ToLowerInvariant());
        RequestQuery.Add(query, "candle", request.Candle?.ToString().ToLowerInvariant());
        RequestQuery.Add(query, "52week", request.Week52?.ToString().ToLowerInvariant());

        var response = await _apiClient.GetAsync(
            $"stocks/quotes/{Uri.EscapeDataString(request.Symbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new StockQuote(
                    row.String("symbol"),
                    row.Decimal("ask"),
                    row.Long("askSize"),
                    row.Decimal("bid"),
                    row.Long("bidSize"),
                    row.Decimal("mid"),
                    row.Decimal("last"),
                    row.Decimal("change"),
                    row.Double("changepct"),
                    row.Long("volume"),
                    row.Timestamp("updated"),
                    row.Decimal("o"),
                    row.Decimal("h"),
                    row.Decimal("l"),
                    row.Decimal("c"),
                    row.Decimal("52weekHigh"),
                    row.Decimal("52weekLow")),
                "symbol", "ask", "askSize", "bid", "bidSize", "mid", "last", "change",
                "changepct", "volume", "updated", "o", "h", "l", "c", "52weekHigh", "52weekLow"),
            Array.Empty<StockQuote>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockQuotesResponse, IReadOnlyList<StockQuote>>(response, values);
    }

    /// <summary>Gets last prices for multiple stock symbols in one request.</summary>
    /// <param name="symbols">The ticker symbols to price.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One price row per symbol; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is empty or contains a blank symbol.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetPricesAsync(["AAPL", "MSFT"]);
    /// foreach (var price in response.Values)
    ///     Console.WriteLine($"{price.Symbol}: {price.Mid}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public Task<StockPricesResponse> GetPricesAsync(string[] symbols, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPricesAsync(new StockPricesRequest(symbols), options, cancellationToken);

    /// <summary>Gets last prices for multiple stock symbols in one request.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One price row per symbol; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public async Task<StockPricesResponse> GetPricesAsync(
        StockPricesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.Add(query, "symbols", string.Join(",", request.Symbols));
        var response = await _apiClient.GetAsync("stocks/prices", true, query, cancellationToken)
            .ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new StockPrice(
                    row.String("symbol"),
                    row.Decimal("mid"),
                    row.Decimal("change"),
                    row.Double("changepct"),
                    row.Timestamp("updated")),
                "symbol", "mid", "change", "changepct", "updated"),
            Array.Empty<StockPrice>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockPricesResponse, IReadOnlyList<StockPrice>>(response, values);
    }

    /// <summary>Gets the latest price for one stock symbol using the path-based endpoint.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The single price row; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetPriceAsync("AAPL");
    /// Console.WriteLine(response.Values[0].Mid);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public Task<StockPricesResponse> GetPriceAsync(string symbol, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPriceAsync(new StockPriceRequest(symbol), options, cancellationToken);

    /// <summary>Gets the latest price for one stock symbol using the path-based endpoint.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The single price row; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public async Task<StockPricesResponse> GetPriceAsync(
        StockPriceRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var response = await _apiClient.GetAsync(
            $"stocks/prices/{Uri.EscapeDataString(request.Symbol)}",
            true,
            RequestQuery.From(effective),
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            ParsePrices,
            Array.Empty<StockPrice>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockPricesResponse, IReadOnlyList<StockPrice>>(response, values);
    }

    /// <summary>Gets quotes for multiple stock symbols in one request.</summary>
    /// <param name="symbols">The ticker symbols to quote.</param>
    /// <param name="extended">Include extended-hours (pre/post market) data.</param>
    /// <param name="candle">Include the current session's OHLC fields on each quote.</param>
    /// <param name="week52">Include the 52-week high and low on each quote.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One quote row per symbol; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is empty or contains a blank symbol.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetQuotesAsync(["AAPL", "MSFT", "NVDA"]);
    /// foreach (var quote in response.Values)
    ///     Console.WriteLine($"{quote.Symbol}: {quote.Mid}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public Task<StockQuotesResponse> GetQuotesAsync(string[] symbols, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesAsync(new StockQuotesRequest(symbols) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Gets quotes for multiple stock symbols in one request.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One quote row per symbol; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public async Task<StockQuotesResponse> GetQuotesAsync(
        StockQuotesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.Add(query, "symbols", string.Join(",", request.Symbols));
        AddBoolean(query, "extended", request.Extended);
        AddBoolean(query, "candle", request.Candle);
        AddBoolean(query, "52week", request.Week52);
        var response = await _apiClient.GetAsync("stocks/quotes", true, query, cancellationToken)
            .ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            ParseQuotes,
            Array.Empty<StockQuote>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockQuotesResponse, IReadOnlyList<StockQuote>>(response, values);
    }

    /// <summary>Gets OHLCV candles for a stock symbol. Intraday windows longer than one year are fetched in one-year chunks and merged; <c>Parts</c> exposes the constituent requests.</summary>
    /// <param name="resolution">Candle duration, e.g. <c>StockResolution.Daily</c> or <c>StockResolution.Minutes(5)</c>.</param>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single trading day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of candles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="exchange">Exchange to pull candles from.</param>
    /// <param name="extended">Include extended-hours candles (intraday resolutions).</param>
    /// <param name="country">Two-letter country code for non-US listings.</param>
    /// <param name="adjustSplits">Adjust prices for splits.</param>
    /// <param name="adjustDividends">Adjust prices for dividends.</param>
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
    /// var response = await client.Stocks.GetCandlesAsync(StockResolution.Daily, "AAPL", countback: 30);
    /// Console.WriteLine(response.Values[0].Close);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/candles/"/>
    public Task<StockCandlesResponse> GetCandlesAsync(StockResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? exchange = null, bool? extended = null, string? country = null, bool? adjustSplits = null, bool? adjustDividends = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesAsync(new StockCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback, Exchange = exchange, Extended = extended, Country = country, AdjustSplits = adjustSplits, AdjustDividends = adjustDividends }, options, cancellationToken);

    /// <summary>Gets OHLCV candles for a stock symbol. Intraday windows longer than one year are fetched in one-year chunks and merged; <c>Parts</c> exposes the constituent requests.</summary>
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
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/candles/"/>
    public async Task<StockCandlesResponse> GetCandlesAsync(
        StockCandlesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var chunks = CandleChunks(request, _apiClient.TimeProvider);
        if (chunks.Count == 1)
        {
            return await GetCandlesResponseAsync(
                request, request.From, request.To, effective, cancellationToken).ConfigureAwait(false);
        }

        var responses = await Task.WhenAll(
            chunks.Select(chunk => GetCandlesResponseAsync(
                request, chunk.From, chunk.To, effective, cancellationToken))).ConfigureAwait(false);
        // Defense in depth on top of half-open chunking: a bar can never appear twice even if the
        // server returns overlapping windows. Bars without a timestamp are indistinguishable, so
        // DistinctBy keeps the first; the server contract always populates t.
        var merged = responses
            .SelectMany(response => response.Values)
            .DistinctBy(candle => candle.Time)
            .ToArray();
        var path = CandlePath(request);
        var logicalRequestUrl = _apiClient.CreateRequestUri(
            path,
            versioned: true,
            CreateCandleQuery(request, request.From, request.To, effective, csv: false));
        return JsonResponseParser.CreateCompositeResponse<StockCandlesResponse, IReadOnlyList<StockCandle>>(
            merged,
            logicalRequestUrl,
            CompositeStatusCode(responses.Select(response => (response.IsNoData, response.StatusCode))),
            Array.Empty<byte>(),
            responses.SelectMany(response => response.Parts).ToArray());
    }

    /// <summary>Gets news articles for a stock symbol.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single day of news to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of articles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The articles plus the <c>Updated</c> timestamp; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetNewsAsync("AAPL", countback: 3);
    /// foreach (var article in response.Values)
    ///     Console.WriteLine($"{article.PublicationDate:d}  {article.Headline}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/news/"/>
    public Task<StockNewsResponse> GetNewsAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetNewsAsync(new StockNewsRequest(symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets news articles for a stock symbol.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The articles plus the <c>Updated</c> timestamp; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request's date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/news/"/>
    public async Task<StockNewsResponse> GetNewsAsync(
        StockNewsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        var response = await _apiClient.GetAsync(
            $"stocks/news/{Uri.EscapeDataString(request.Symbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var result = JsonResponseParser.DecodeOrDefault(
            response,
            root =>
            {
                var articles = JsonResponseParser.ReadParallelArray(
                    root,
                    row => new StockNewsArticle(
                        row.String("symbol") ?? throw new JsonException("Missing symbol."),
                        row.String("headline") ?? throw new JsonException("Missing headline."),
                        row.String("content") ?? throw new JsonException("Missing content."),
                        row.String("source") ?? throw new JsonException("Missing source."),
                        row.Timestamp("publicationDate") ?? throw new JsonException("Missing publicationDate.")),
                    "symbol", "headline", "content", "source", "publicationDate");
                return (Articles: (IReadOnlyList<StockNewsArticle>)articles, Updated: JsonResponseParser.Timestamp(root, "updated"));
            },
            (Articles: (IReadOnlyList<StockNewsArticle>)Array.Empty<StockNewsArticle>(), Updated: (DateTimeOffset?)null),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockNewsResponse, IReadOnlyList<StockNewsArticle>>(
            response,
            result.Articles,
            typedResponse =>
            {
                typedResponse.Updated = result.Updated;
                return typedResponse;
            });
    }

    /// <summary>Gets historical and forward earnings data for a stock symbol.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of reports counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="report">Fetch one specific report period (e.g. <c>2023-Q4</c>); cannot be combined with any date-window field.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One row per earnings report; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank, <paramref name="report"/> is combined with a date-window field, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetEarningsAsync("AAPL", from: new DateOnly(2023, 1, 1), to: new DateOnly(2023, 12, 31));
    /// Console.WriteLine(response.Values[0].ReportedEps);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/earnings/"/>
    public Task<StockEarningsResponse> GetEarningsAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? report = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetEarningsAsync(new StockEarningsRequest(symbol) { Date = date, From = from, To = to, Countback = countback, Report = report }, options, cancellationToken);

    /// <summary>Gets historical and forward earnings data for a stock symbol.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One row per earnings report; when the API reports no data, <see cref="MarketDataResponse{T}.IsNoData"/> is <c>true</c> and <c>Values</c> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request combines <c>Report</c> with a date-window field, or its date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/earnings/"/>
    public async Task<StockEarningsResponse> GetEarningsAsync(
        StockEarningsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEarningsRequest(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        RequestQuery.Add(query, "report", request.Report);
        var response = await _apiClient.GetAsync(
            $"stocks/earnings/{Uri.EscapeDataString(request.Symbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new StockEarning(
                    row.String("symbol"),
                    ToInt(row.Long("fiscalYear")),
                    ToInt(row.Long("fiscalQuarter")),
                    row.Timestamp("date"),
                    row.Timestamp("reportDate"),
                    row.String("reportTime"),
                    row.String("currency"),
                    row.Decimal("reportedEPS"),
                    row.Decimal("estimatedEPS"),
                    row.Decimal("surpriseEPS"),
                    row.Double("surpriseEPSpct"),
                    row.Timestamp("updated")),
                "symbol", "fiscalYear", "fiscalQuarter", "date", "reportDate", "reportTime",
                "currency", "reportedEPS", "estimatedEPS", "surpriseEPS", "surpriseEPSpct", "updated"),
            Array.Empty<StockEarning>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockEarningsResponse, IReadOnlyList<StockEarning>>(response, values);
    }

    /// <summary>Gets a real-time or delayed quote for one stock symbol as CSV.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="extended">Include extended-hours (pre/post market) data.</param>
    /// <param name="candle">Include the current session's OHLC fields on the quote.</param>
    /// <param name="week52">Include the 52-week high and low on the quote.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetQuoteCsvAsync("AAPL");
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public Task<CsvResponse> GetQuoteCsvAsync(string symbol, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteCsvAsync(new StockQuoteRequest(symbol) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Gets a real-time or delayed quote for one stock symbol as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public async Task<CsvResponse> GetQuoteCsvAsync(
        StockQuoteRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        AddBoolean(query, "extended", request.Extended);
        AddBoolean(query, "candle", request.Candle);
        AddBoolean(query, "52week", request.Week52);
        return await GetCsvAsync(
            $"stocks/quotes/{Uri.EscapeDataString(request.Symbol)}", query, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets last prices for multiple stock symbols in one request as CSV.</summary>
    /// <param name="symbols">The ticker symbols to price.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is empty or contains a blank symbol.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetPricesCsvAsync(["AAPL", "MSFT"]);
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public Task<CsvResponse> GetPricesCsvAsync(string[] symbols, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPricesCsvAsync(new StockPricesRequest(symbols), options, cancellationToken);

    /// <summary>Gets last prices for multiple stock symbols in one request as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public async Task<CsvResponse> GetPricesCsvAsync(
        StockPricesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.Add(query, "symbols", string.Join(",", request.Symbols));
        return await GetCsvAsync("stocks/prices", query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the latest price for one stock symbol as CSV using the path-based endpoint.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetPriceCsvAsync("AAPL");
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public Task<CsvResponse> GetPriceCsvAsync(string symbol, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPriceCsvAsync(new StockPriceRequest(symbol), options, cancellationToken);

    /// <summary>Gets the latest price for one stock symbol as CSV using the path-based endpoint.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/prices/"/>
    public async Task<CsvResponse> GetPriceCsvAsync(
        StockPriceRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await GetCsvAsync(
            $"stocks/prices/{Uri.EscapeDataString(request.Symbol)}",
            RequestQuery.Csv(_apiClient.ApplyDefaults(options)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets quotes for multiple stock symbols in one request as CSV.</summary>
    /// <param name="symbols">The ticker symbols to quote.</param>
    /// <param name="extended">Include extended-hours (pre/post market) data.</param>
    /// <param name="candle">Include the current session's OHLC fields on each quote.</param>
    /// <param name="week52">Include the 52-week high and low on each quote.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbols"/> is empty or contains a blank symbol.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetQuotesCsvAsync(["AAPL", "MSFT"]);
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public Task<CsvResponse> GetQuotesCsvAsync(string[] symbols, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesCsvAsync(new StockQuotesRequest(symbols) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Gets quotes for multiple stock symbols in one request as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/quotes/"/>
    public async Task<CsvResponse> GetQuotesCsvAsync(
        StockQuotesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.Add(query, "symbols", string.Join(",", request.Symbols));
        AddBoolean(query, "extended", request.Extended);
        AddBoolean(query, "candle", request.Candle);
        AddBoolean(query, "52week", request.Week52);
        return await GetCsvAsync("stocks/quotes", query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets OHLCV candles for a stock symbol as CSV. Intraday windows longer than one year are fetched in one-year chunks and merged; <c>Parts</c> exposes the constituent requests.</summary>
    /// <param name="resolution">Candle duration, e.g. <c>StockResolution.Daily</c> or <c>StockResolution.Minutes(5)</c>.</param>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single trading day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of candles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="exchange">Exchange to pull candles from.</param>
    /// <param name="extended">Include extended-hours candles (intraday resolutions).</param>
    /// <param name="country">Two-letter country code for non-US listings.</param>
    /// <param name="adjustSplits">Adjust prices for splits.</param>
    /// <param name="adjustDividends">Adjust prices for dividends.</param>
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
    /// var response = await client.Stocks.GetCandlesCsvAsync(StockResolution.Daily, "AAPL", countback: 30);
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/candles/"/>
    public Task<CsvResponse> GetCandlesCsvAsync(StockResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? exchange = null, bool? extended = null, string? country = null, bool? adjustSplits = null, bool? adjustDividends = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesCsvAsync(new StockCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback, Exchange = exchange, Extended = extended, Country = country, AdjustSplits = adjustSplits, AdjustDividends = adjustDividends }, options, cancellationToken);

    /// <summary>Gets OHLCV candles for a stock symbol as CSV. Intraday windows longer than one year are fetched in one-year chunks and merged; <c>Parts</c> exposes the constituent requests.</summary>
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
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/candles/"/>
    public async Task<CsvResponse> GetCandlesCsvAsync(
        StockCandlesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var chunks = CandleChunks(request, _apiClient.TimeProvider);
        if (chunks.Count == 1)
        {
            return await GetCsvAsync(
                CandlePath(request),
                CreateCandleQuery(request, request.From, request.To, effective, csv: true),
                cancellationToken).ConfigureAwait(false);
        }

        var responses = await Task.WhenAll(chunks.Select(chunk => GetCsvAsync(
            CandlePath(request),
            CreateCandleQuery(request, chunk.From, chunk.To, effective, csv: true),
            cancellationToken))).ConfigureAwait(false);
        var mergedCsv = MergeCandleCsv(responses, effective.Headers is not false);
        var logicalRequestUrl = _apiClient.CreateRequestUri(
            CandlePath(request),
            versioned: true,
            CreateCandleQuery(request, request.From, request.To, effective, csv: true));
        return JsonResponseParser.CreateCompositeResponse<CsvResponse, string>(
            mergedCsv,
            logicalRequestUrl,
            CompositeStatusCode(responses.Select(response => (response.IsNoData, response.StatusCode))),
            Encoding.UTF8.GetBytes(mergedCsv),
            responses.SelectMany(response => response.Parts).ToArray());
    }

    /// <summary>Gets news articles for a stock symbol as CSV.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single day of news to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of articles counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
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
    /// var response = await client.Stocks.GetNewsCsvAsync("AAPL", countback: 3);
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/news/"/>
    public Task<CsvResponse> GetNewsCsvAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetNewsCsvAsync(new StockNewsRequest(symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Gets news articles for a stock symbol as CSV.</summary>
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
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/news/"/>
    public async Task<CsvResponse> GetNewsCsvAsync(
        StockNewsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        return await GetCsvAsync(
            $"stocks/news/{Uri.EscapeDataString(request.Symbol)}", query, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets historical and forward earnings data for a stock symbol as CSV.</summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="date">Single day to fetch; mutually exclusive with the other window fields.</param>
    /// <param name="from">Start of the date window (inclusive).</param>
    /// <param name="to">End of the date window (inclusive).</param>
    /// <param name="countback">Number of reports counting back from <paramref name="to"/> (or today); cannot be combined with <paramref name="from"/>.</param>
    /// <param name="report">Fetch one specific report period (e.g. <c>2023-Q4</c>); cannot be combined with any date-window field.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null or blank, <paramref name="report"/> is combined with a date-window field, or the date window is invalid (<paramref name="date"/> combined with <paramref name="from"/>/<paramref name="to"/>, <paramref name="countback"/> combined with <paramref name="from"/>, or <paramref name="from"/> after <paramref name="to"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="countback"/> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <example><code>
    /// var response = await client.Stocks.GetEarningsCsvAsync("AAPL", report: "2023-Q4");
    /// Console.WriteLine(response.Csv);
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/earnings/"/>
    public Task<CsvResponse> GetEarningsCsvAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? report = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetEarningsCsvAsync(new StockEarningsRequest(symbol) { Date = date, From = from, To = to, Countback = countback, Report = report }, options, cancellationToken);

    /// <summary>Gets historical and forward earnings data for a stock symbol as CSV.</summary>
    /// <param name="request">The endpoint parameters.</param>
    /// <param name="options">Optional per-request overrides merged over the client defaults.</param>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The raw CSV payload plus response metadata (request id, status, rate limit).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The request combines <c>Report</c> with a date-window field, or its date window is invalid (<c>Date</c> combined with <c>From</c>/<c>To</c>, <c>Countback</c> combined with <c>From</c>, or <c>From</c> after <c>To</c>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The request's <c>Countback</c> is zero or negative.</exception>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/stocks/earnings/"/>
    public async Task<CsvResponse> GetEarningsCsvAsync(
        StockEarningsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEarningsRequest(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, request.Countback);
        RequestQuery.Add(query, "report", request.Report);
        return await GetCsvAsync(
            $"stocks/earnings/{Uri.EscapeDataString(request.Symbol)}", query, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CsvResponse> GetCsvAsync(
        string path,
        IEnumerable<KeyValuePair<string, string?>> query,
        CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync(path, true, query, cancellationToken)
            .ConfigureAwait(false);
        return JsonResponseParser.CreateCsvResponse(response);
    }

    private async Task<StockCandlesResponse> GetCandlesResponseAsync(
        StockCandlesRequest request,
        DateOnly? from,
        DateOnly? to,
        MarketDataRequestOptions effective,
        CancellationToken cancellationToken)
    {
        var query = CreateCandleQuery(request, from, to, effective, csv: false);
        var path = CandlePath(request);
        var response = await _apiClient.GetAsync(path, true, query, cancellationToken)
            .ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new StockCandle(
                    row.Timestamp("t"),
                    row.Decimal("o"),
                    row.Decimal("h"),
                    row.Decimal("l"),
                    row.Decimal("c"),
                    row.Long("v")),
                "t", "o", "h", "l", "c", "v"),
            Array.Empty<StockCandle>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<StockCandlesResponse, IReadOnlyList<StockCandle>>(response, values);
    }

    private static List<KeyValuePair<string, string?>> CreateCandleQuery(
        StockCandlesRequest request,
        DateOnly? from,
        DateOnly? to,
        MarketDataRequestOptions effective,
        bool csv)
    {
        var query = csv ? RequestQuery.Csv(effective) : RequestQuery.From(effective);
        RequestQuery.AddDateWindow(query, request.Date, from, to, request.Countback);
        RequestQuery.Add(query, "exchange", request.Exchange);
        AddBoolean(query, "extended", request.Extended);
        RequestQuery.Add(query, "country", request.Country);
        AddBoolean(query, "adjustsplits", request.AdjustSplits);
        AddBoolean(query, "adjustdividends", request.AdjustDividends);
        return query;
    }

    private static string CandlePath(StockCandlesRequest request) =>
        $"stocks/candles/{Uri.EscapeDataString(request.Resolution.WireValue)}/{Uri.EscapeDataString(request.Symbol)}";

    private static int CompositeStatusCode(IEnumerable<(bool IsNoData, int StatusCode)> responses)
    {
        var parts = responses.ToArray();
        if (parts.All(response => response.IsNoData))
        {
            return 404;
        }

        return parts.Any(response => response.StatusCode == 203) ? 203 : 200;
    }

    private static string MergeCandleCsv(IReadOnlyList<CsvResponse> responses, bool includesHeaders)
    {
        var builder = new StringBuilder();
        var headerWritten = false;
        foreach (var response in responses)
        {
            var csv = response.Csv;
            if (csv.Length == 0)
            {
                continue;
            }

            if (headerWritten && includesHeaders)
            {
                var newline = csv.IndexOf('\n');
                csv = newline < 0 ? string.Empty : csv[(newline + 1)..];
            }

            if (csv.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0 && builder[^1] is not ('\r' or '\n'))
            {
                builder.AppendLine();
            }

            builder.Append(csv);
            headerWritten = true;
        }

        return builder.ToString();
    }

    private sealed record CandleDateRange(DateOnly? From, DateOnly? To);

    private static IReadOnlyList<CandleDateRange> CandleChunks(StockCandlesRequest request, TimeProvider timeProvider)
    {
        if (request.From is not { } from
            || !request.Resolution.IsIntraday
            || request.To is { } explicitTo && from.AddDays(365) >= explicitTo)
        {
            return [new CandleDateRange(request.From, request.To)];
        }

        var to = request.To ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (from >= to)
        {
            return [new CandleDateRange(from, to)];
        }

        var ranges = new List<CandleDateRange>();
        var current = from;
        while (current < to)
        {
            var next = current.AddDays(365);
            if (next > to) next = to;
            ranges.Add(new CandleDateRange(current, next));
            // from/to are inclusive on the wire, so the next chunk starts the day AFTER this
            // chunk's end; sharing the boundary day would fetch (and bill) its bars twice.
            current = next.AddDays(1);
        }

        return ranges;
    }

    private static void ValidateEarningsRequest(StockEarningsRequest request)
    {
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, request.Countback, nameof(request));
        if (!string.IsNullOrWhiteSpace(request.Report)
            && (request.Date.HasValue || request.From.HasValue || request.To.HasValue || request.Countback.HasValue))
        {
            throw new ArgumentException(
                "Report cannot be combined with Date, From, To, or Countback.",
                nameof(request));
        }
    }

    private static IReadOnlyList<StockQuote> ParseQuotes(System.Text.Json.JsonElement root) =>
        JsonResponseParser.ReadParallelArray(
            root,
            row => new StockQuote(
                row.String("symbol"),
                row.Decimal("ask"),
                row.Long("askSize"),
                row.Decimal("bid"),
                row.Long("bidSize"),
                row.Decimal("mid"),
                row.Decimal("last"),
                row.Decimal("change"),
                row.Double("changepct"),
                row.Long("volume"),
                row.Timestamp("updated"),
                row.Decimal("o"),
                row.Decimal("h"),
                row.Decimal("l"),
                row.Decimal("c"),
                row.Decimal("52weekHigh"),
                row.Decimal("52weekLow")),
            "symbol", "ask", "askSize", "bid", "bidSize", "mid", "last", "change",
            "changepct", "volume", "updated", "o", "h", "l", "c", "52weekHigh", "52weekLow");

    private static IReadOnlyList<StockPrice> ParsePrices(System.Text.Json.JsonElement root) =>
        JsonResponseParser.ReadParallelArray(
            root,
            row => new StockPrice(
                row.String("symbol"),
                row.Decimal("mid"),
                row.Decimal("change"),
                row.Double("changepct"),
                row.Timestamp("updated")),
            "symbol", "mid", "change", "changepct", "updated");

    private static void AddBoolean(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        bool? value) =>
        RequestQuery.Add(query, name, value?.ToString().ToLowerInvariant());

    private static int? ToInt(long? value) => value is null ? null : checked((int)value.Value);
}

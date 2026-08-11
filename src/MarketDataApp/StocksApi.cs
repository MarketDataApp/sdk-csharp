using MarketDataApp.Stocks;
using System.Text;
using System.Text.Json;

namespace MarketDataApp;

/// <summary>Asynchronous stock endpoints.</summary>
public sealed class StocksApi
{
    private readonly ApiClient _apiClient;

    internal StocksApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets a real-time or historical quote for one stock symbol.</summary>
    public Task<StockQuotesResponse> GetQuoteAsync(string symbol, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteAsync(new StockQuoteRequest(symbol) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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
    public Task<StockPricesResponse> GetPricesAsync(string[] symbols, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPricesAsync(new StockPricesRequest(symbols), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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
    public Task<StockPricesResponse> GetPriceAsync(string symbol, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPriceAsync(new StockPriceRequest(symbol), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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
    public Task<StockQuotesResponse> GetQuotesAsync(string[] symbols, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesAsync(new StockQuotesRequest(symbols) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets OHLCV candles for a stock symbol.</summary>
    public Task<StockCandlesResponse> GetCandlesAsync(StockResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? exchange = null, bool? extended = null, string? country = null, bool? adjustSplits = null, bool? adjustDividends = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesAsync(new StockCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback, Exchange = exchange, Extended = extended, Country = country, AdjustSplits = adjustSplits, AdjustDividends = adjustDividends }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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
    public Task<StockNewsResponse> GetNewsAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetNewsAsync(new StockNewsRequest(symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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
    public Task<StockEarningsResponse> GetEarningsAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? report = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetEarningsAsync(new StockEarningsRequest(symbol) { Date = date, From = from, To = to, Countback = countback, Report = report }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets a CSV quote for one stock symbol.</summary>
    public Task<CsvResponse> GetQuoteCsvAsync(string symbol, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteCsvAsync(new StockQuoteRequest(symbol) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets CSV prices for multiple stock symbols.</summary>
    public Task<CsvResponse> GetPricesCsvAsync(string[] symbols, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPricesCsvAsync(new StockPricesRequest(symbols), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets a CSV price for one stock symbol using the path-based endpoint.</summary>
    public Task<CsvResponse> GetPriceCsvAsync(string symbol, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetPriceCsvAsync(new StockPriceRequest(symbol), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets CSV quotes for multiple stock symbols.</summary>
    public Task<CsvResponse> GetQuotesCsvAsync(string[] symbols, bool? extended = null, bool? candle = null, bool? week52 = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesCsvAsync(new StockQuotesRequest(symbols) { Extended = extended, Candle = candle, Week52 = week52 }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets CSV OHLCV candles for a stock symbol.</summary>
    public Task<CsvResponse> GetCandlesCsvAsync(StockResolution resolution, string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? exchange = null, bool? extended = null, string? country = null, bool? adjustSplits = null, bool? adjustDividends = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCandlesCsvAsync(new StockCandlesRequest(resolution, symbol) { Date = date, From = from, To = to, Countback = countback, Exchange = exchange, Extended = extended, Country = country, AdjustSplits = adjustSplits, AdjustDividends = adjustDividends }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets CSV news articles for a stock symbol.</summary>
    public Task<CsvResponse> GetNewsCsvAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetNewsCsvAsync(new StockNewsRequest(symbol) { Date = date, From = from, To = to, Countback = countback }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

    /// <summary>Gets CSV earnings data for a stock symbol.</summary>
    public Task<CsvResponse> GetEarningsCsvAsync(string symbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, int? countback = null, string? report = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetEarningsCsvAsync(new StockEarningsRequest(symbol) { Date = date, From = from, To = to, Countback = countback, Report = report }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
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

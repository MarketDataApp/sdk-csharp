using System.Globalization;
using System.Text.Json;
using MarketDataApp.Options;

namespace MarketDataApp;

/// <summary>Asynchronous options endpoints.</summary>
public sealed class OptionsApi
{
    private readonly ApiClient _apiClient;

    internal OptionsApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Resolves user input to a canonical OCC option symbol.</summary>
    public Task<OptionsLookupResponse> GetLookupAsync(string userInput, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetLookupAsync(new OptionsLookupRequest(userInput), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<OptionsLookupResponse> GetLookupAsync(
        OptionsLookupRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var response = await _apiClient.GetAsync(
            $"options/lookup/{Uri.EscapeDataString(request.UserInput)}",
            true,
            RequestQuery.From(effective),
            cancellationToken).ConfigureAwait(false);
        var value = JsonResponseParser.DecodeOrDefault(
            response,
            root => root.TryGetProperty("optionSymbol", out var symbol)
                && symbol.ValueKind == JsonValueKind.String
                ? symbol.GetString()!
                : throw new JsonException("Missing optionSymbol."),
            string.Empty,
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<OptionsLookupResponse, string>(response, value);
    }

    /// <summary>Gets available expiration dates for an underlying symbol.</summary>
    public Task<OptionsExpirationsResponse> GetExpirationsAsync(string symbol, decimal? strike = null, DateOnly? date = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetExpirationsAsync(new OptionsExpirationsRequest(symbol) { Strike = strike, Date = date }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<OptionsExpirationsResponse> GetExpirationsAsync(
        OptionsExpirationsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        Add(query, "strike", request.Strike);
        AddDate(query, "date", request.Date);
        var response = await _apiClient.GetAsync(
            $"options/expirations/{Uri.EscapeDataString(request.Symbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var result = JsonResponseParser.DecodeOrDefault(
            response,
            root =>
            {
                var expirations = JsonResponseParser.ReadParallelArray(
                    root,
                    row => row.Timestamp("expirations")
                        ?? throw new JsonException("Missing expirations."),
                    "expirations");
                return (Values: expirations, Updated: JsonResponseParser.Timestamp(root, "updated"));
            },
            (Values: (IReadOnlyList<DateTimeOffset>)Array.Empty<DateTimeOffset>(), Updated: (DateTimeOffset?)null),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<OptionsExpirationsResponse, IReadOnlyList<DateTimeOffset>>(
            response,
            result.Values,
            typedResponse =>
            {
                typedResponse.Updated = result.Updated;
                return typedResponse;
            });
    }

    /// <summary>Gets historical or current quotes for one OCC option symbol.</summary>
    public Task<OptionsQuotesResponse> GetQuoteAsync(string optionSymbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteAsync(new OptionsQuoteRequest(optionSymbol) { Date = date, From = from, To = to }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<OptionsQuotesResponse> GetQuoteAsync(
        OptionsQuoteRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, countback: null, nameof(request));
        var response = await GetQuoteResponseAsync(
            request.OptionSymbol,
            request.Date,
            request.From,
            request.To,
            _apiClient.ApplyDefaults(options),
            cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Gets quotes for multiple OCC option symbols. The API is called once per symbol concurrently.
    /// </summary>
    public Task<IReadOnlyDictionary<string, OptionsQuotesResponse>> GetQuotesAsync(string[] optionSymbols, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesAsync(new OptionsQuotesRequest(optionSymbols) { Date = date, From = from, To = to }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<IReadOnlyDictionary<string, OptionsQuotesResponse>> GetQuotesAsync(
        OptionsQuotesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, countback: null, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var tasks = request.OptionSymbols.Select(async symbol =>
        {
            var response = await GetQuoteResponseAsync(
                symbol,
                request.Date,
                request.From,
                request.To,
                effective,
                cancellationToken).ConfigureAwait(false);
            return (symbol, response);
        });
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(item => item.symbol, item => item.response);
    }

    /// <summary>Gets the options chain for an underlying symbol.</summary>
    public Task<OptionsChainResponse> GetChainAsync(string symbol, ExpirationFilter? expiration = null, bool? weekly = null, bool? monthly = null, bool? quarterly = null, bool? am = null, bool? pm = null, bool? nonStandard = null, StrikeFilter? strike = null, double? delta = null, int? strikeLimit = null, StrikeRange? strikeRangeFilter = null, decimal? minBid = null, decimal? maxBid = null, decimal? minAsk = null, decimal? maxAsk = null, decimal? maxBidAskSpread = null, double? maxBidAskSpreadPct = null, long? minOpenInterest = null, long? minVolume = null, OptionSide? side = null, DateOnly? date = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetChainAsync(new OptionsChainRequest(symbol) { Expiration = expiration, Weekly = weekly, Monthly = monthly, Quarterly = quarterly, Am = am, Pm = pm, NonStandard = nonStandard, Strike = strike, Delta = delta, StrikeLimit = strikeLimit, StrikeRangeFilter = strikeRangeFilter, MinBid = minBid, MaxBid = maxBid, MinAsk = minAsk, MaxAsk = maxAsk, MaxBidAskSpread = maxBidAskSpread, MaxBidAskSpreadPct = maxBidAskSpreadPct, MinOpenInterest = minOpenInterest, MinVolume = minVolume, Side = side, Date = date }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<OptionsChainResponse> GetChainAsync(
        OptionsChainRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateChainRequest(request);
        var effective = _apiClient.ApplyDefaults(options);
        var query = RequestQuery.From(effective);
        AddExpirationFilter(query, request.Expiration);
        AddBoolean(query, "weekly", request.Weekly);
        AddBoolean(query, "monthly", request.Monthly);
        AddBoolean(query, "quarterly", request.Quarterly);
        AddBoolean(query, "am", request.Am);
        AddBoolean(query, "pm", request.Pm);
        AddBoolean(query, "nonstandard", request.NonStandard);
        RequestQuery.Add(query, "strike", FormatStrikeFilter(request.Strike));
        Add(query, "delta", request.Delta);
        Add(query, "strikeLimit", request.StrikeLimit);
        RequestQuery.Add(query, "range", request.StrikeRangeFilter?.ToWireValue());
        Add(query, "minBid", request.MinBid);
        Add(query, "maxBid", request.MaxBid);
        Add(query, "minAsk", request.MinAsk);
        Add(query, "maxAsk", request.MaxAsk);
        Add(query, "maxBidAskSpread", request.MaxBidAskSpread);
        Add(query, "maxBidAskSpreadPct", request.MaxBidAskSpreadPct);
        Add(query, "minOpenInterest", request.MinOpenInterest);
        Add(query, "minVolume", request.MinVolume);
        RequestQuery.Add(query, "side", request.Side?.ToWireValue());
        AddDate(query, "date", request.Date);

        var response = await _apiClient.GetAsync(
            $"options/chain/{Uri.EscapeDataString(request.Symbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            ParseOptionQuotes,
            Array.Empty<OptionQuote>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<OptionsChainResponse, IReadOnlyList<OptionQuote>>(response, values);
    }

    /// <summary>Resolves user input to a canonical OCC option symbol as CSV.</summary>
    public Task<CsvResponse> GetLookupCsvAsync(string userInput, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetLookupCsvAsync(new OptionsLookupRequest(userInput), options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetLookupCsvAsync(
        OptionsLookupRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await GetCsvAsync(
            $"options/lookup/{Uri.EscapeDataString(request.UserInput)}",
            RequestQuery.Csv(_apiClient.ApplyDefaults(options)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets available expiration dates for an underlying symbol as CSV.</summary>
    public Task<CsvResponse> GetExpirationsCsvAsync(string symbol, decimal? strike = null, DateOnly? date = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetExpirationsCsvAsync(new OptionsExpirationsRequest(symbol) { Strike = strike, Date = date }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetExpirationsCsvAsync(
        OptionsExpirationsRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        Add(query, "strike", request.Strike);
        AddDate(query, "date", request.Date);
        return await GetCsvAsync(
            $"options/expirations/{Uri.EscapeDataString(request.Symbol)}", query, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets historical or current quotes for one OCC option symbol as CSV.</summary>
    public Task<CsvResponse> GetQuoteCsvAsync(string optionSymbol, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuoteCsvAsync(new OptionsQuoteRequest(optionSymbol) { Date = date, From = from, To = to }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetQuoteCsvAsync(
        OptionsQuoteRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, countback: null, nameof(request));
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, countback: null);
        return await GetCsvAsync(
            $"options/quotes/{Uri.EscapeDataString(request.OptionSymbol)}", query, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets quotes for multiple OCC option symbols as CSV, one response per symbol.</summary>
    public Task<IReadOnlyDictionary<string, CsvResponse>> GetQuotesCsvAsync(string[] optionSymbols, DateOnly? date = null, DateOnly? from = null, DateOnly? to = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetQuotesCsvAsync(new OptionsQuotesRequest(optionSymbols) { Date = date, From = from, To = to }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<IReadOnlyDictionary<string, CsvResponse>> GetQuotesCsvAsync(
        OptionsQuotesRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.ValidateDateWindow(
            request.Date, request.From, request.To, countback: null, nameof(request));
        var effective = _apiClient.ApplyDefaults(options);
        var tasks = request.OptionSymbols.Select(async symbol =>
        {
            var query = RequestQuery.Csv(effective);
            RequestQuery.AddDateWindow(query, request.Date, request.From, request.To, countback: null);
            var response = await GetCsvAsync(
                $"options/quotes/{Uri.EscapeDataString(symbol)}", query, cancellationToken)
                .ConfigureAwait(false);
            return (symbol, response);
        });
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(item => item.symbol, item => item.response);
    }

    /// <summary>Gets the options chain for an underlying symbol as CSV.</summary>
    public Task<CsvResponse> GetChainCsvAsync(string symbol, ExpirationFilter? expiration = null, bool? weekly = null, bool? monthly = null, bool? quarterly = null, bool? am = null, bool? pm = null, bool? nonStandard = null, StrikeFilter? strike = null, double? delta = null, int? strikeLimit = null, StrikeRange? strikeRangeFilter = null, decimal? minBid = null, decimal? maxBid = null, decimal? minAsk = null, decimal? maxAsk = null, decimal? maxBidAskSpread = null, double? maxBidAskSpreadPct = null, long? minOpenInterest = null, long? minVolume = null, OptionSide? side = null, DateOnly? date = null, MarketDataRequestOptions? options = null, CancellationToken cancellationToken = default) =>
        GetChainCsvAsync(new OptionsChainRequest(symbol) { Expiration = expiration, Weekly = weekly, Monthly = monthly, Quarterly = quarterly, Am = am, Pm = pm, NonStandard = nonStandard, Strike = strike, Delta = delta, StrikeLimit = strikeLimit, StrikeRangeFilter = strikeRangeFilter, MinBid = minBid, MaxBid = maxBid, MinAsk = minAsk, MaxAsk = maxAsk, MaxBidAskSpread = maxBidAskSpread, MaxBidAskSpreadPct = maxBidAskSpreadPct, MinOpenInterest = minOpenInterest, MinVolume = minVolume, Side = side, Date = date }, options, cancellationToken);

    /// <summary>Executes the endpoint request.</summary>
    public async Task<CsvResponse> GetChainCsvAsync(
        OptionsChainRequest request,
        MarketDataRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateChainRequest(request);
        var query = RequestQuery.Csv(_apiClient.ApplyDefaults(options));
        AddExpirationFilter(query, request.Expiration);
        AddBoolean(query, "weekly", request.Weekly);
        AddBoolean(query, "monthly", request.Monthly);
        AddBoolean(query, "quarterly", request.Quarterly);
        AddBoolean(query, "am", request.Am);
        AddBoolean(query, "pm", request.Pm);
        AddBoolean(query, "nonstandard", request.NonStandard);
        RequestQuery.Add(query, "strike", FormatStrikeFilter(request.Strike));
        Add(query, "delta", request.Delta);
        Add(query, "strikeLimit", request.StrikeLimit);
        RequestQuery.Add(query, "range", request.StrikeRangeFilter?.ToWireValue());
        Add(query, "minBid", request.MinBid);
        Add(query, "maxBid", request.MaxBid);
        Add(query, "minAsk", request.MinAsk);
        Add(query, "maxAsk", request.MaxAsk);
        Add(query, "maxBidAskSpread", request.MaxBidAskSpread);
        Add(query, "maxBidAskSpreadPct", request.MaxBidAskSpreadPct);
        Add(query, "minOpenInterest", request.MinOpenInterest);
        Add(query, "minVolume", request.MinVolume);
        RequestQuery.Add(query, "side", request.Side?.ToWireValue());
        AddDate(query, "date", request.Date);
        return await GetCsvAsync(
            $"options/chain/{Uri.EscapeDataString(request.Symbol)}", query, cancellationToken)
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

    private async Task<OptionsQuotesResponse> GetQuoteResponseAsync(
        string optionSymbol,
        DateOnly? date,
        DateOnly? from,
        DateOnly? to,
        MarketDataRequestOptions effective,
        CancellationToken cancellationToken)
    {
        var query = RequestQuery.From(effective);
        RequestQuery.AddDateWindow(query, date, from, to, countback: null);
        var response = await _apiClient.GetAsync(
            $"options/quotes/{Uri.EscapeDataString(optionSymbol)}",
            true,
            query,
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            ParseOptionQuotes,
            Array.Empty<OptionQuote>(),
            requestedColumns: effective.Columns);
        return JsonResponseParser.CreateResponse<OptionsQuotesResponse, IReadOnlyList<OptionQuote>>(response, values);
    }

    private static IReadOnlyList<OptionQuote> ParseOptionQuotes(JsonElement root) =>
        JsonResponseParser.ReadParallelArray(
            root,
            row => new OptionQuote(
                row.String("optionSymbol"),
                row.String("underlying"),
                row.Timestamp("expiration"),
                row.String("side"),
                row.Decimal("strike"),
                row.Timestamp("firstTraded"),
                ToInt(row.Long("dte")),
                row.Timestamp("updated"),
                row.Decimal("bid"),
                row.Long("bidSize"),
                row.Decimal("mid"),
                row.Decimal("ask"),
                row.Long("askSize"),
                row.Decimal("last"),
                row.Long("openInterest"),
                row.Long("volume"),
                row.Boolean("inTheMoney"),
                row.Decimal("intrinsicValue"),
                row.Decimal("extrinsicValue"),
                row.Decimal("underlyingPrice"),
                row.Double("iv"),
                row.Double("delta"),
                row.Double("gamma"),
                row.Double("theta"),
                row.Double("vega"),
                row.Double("rho")),
            "optionSymbol", "underlying", "expiration", "side", "strike", "firstTraded", "dte",
            "updated", "bid", "bidSize", "mid", "ask", "askSize", "last", "openInterest",
            "volume", "inTheMoney", "intrinsicValue", "extrinsicValue", "underlyingPrice",
            "iv", "delta", "gamma", "theta", "vega", "rho");

    private static void AddExpirationFilter(
        ICollection<KeyValuePair<string, string?>> query,
        ExpirationFilter? filter)
    {
        switch (filter)
        {
            case ExpirationFilter.OnDate onDate:
                AddDate(query, "expiration", onDate.Date);
                break;
            case ExpirationFilter.Dte dte:
                Add(query, "dte", dte.Days);
                break;
            case ExpirationFilter.Between between:
                AddDate(query, "from", between.From);
                AddDate(query, "to", between.To);
                break;
            case ExpirationFilter.MonthYear monthYear:
                Add(query, "month", monthYear.Month);
                Add(query, "year", monthYear.Year);
                break;
            case ExpirationFilter.All:
                RequestQuery.Add(query, "expiration", "all");
                break;
        }
    }

    // Excluded: the final switch arm is an unreachable default. StrikeFilter is an abstract record
    // with an internal constructor and exactly three sealed subtypes (Exact/Range/Comparison), all
    // handled above, so no other StrikeFilter can exist — but the C# switch expression still
    // requires the default arm, which cannot be exercised.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string? FormatStrikeFilter(StrikeFilter? filter) =>
        filter switch
        {
            null => null,
            StrikeFilter.Exact exact => FormatNumber(exact.Price),
            StrikeFilter.Range range => $"{FormatNumber(range.Min)}-{FormatNumber(range.Max)}",
            StrikeFilter.Comparison comparison =>
                $"{comparison.Op.OperatorWireValue()}{FormatNumber(comparison.Price)}",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };

    private static void AddDate(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        DateOnly? value) =>
        RequestQuery.Add(query, name, value is { } date ? RequestQuery.Date(date) : null);

    private static void AddBoolean(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        bool? value) =>
        RequestQuery.Add(query, name, value?.ToString().ToLowerInvariant());

    private static void ValidateChainRequest(OptionsChainRequest request)
    {
        if (request.Delta is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Delta, "Delta must be between -1 and 1.");
        }

        if (request.StrikeLimit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.StrikeLimit,
                "StrikeLimit must be positive.");
        }

        ValidateRange(request.MinBid, request.MaxBid, "bid", nameof(request));
        ValidateRange(request.MinAsk, request.MaxAsk, "ask", nameof(request));
        if (request.MaxBidAskSpread is < 0
            || request.MaxBidAskSpreadPct is < 0
            || request.MinOpenInterest is < 0
            || request.MinVolume is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Spread, open-interest, and volume filters cannot be negative.");
        }
    }

    private static void ValidateRange(decimal? minimum, decimal? maximum, string name, string parameterName)
    {
        if (minimum is < 0 || maximum is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{name} filters cannot be negative.");
        }

        if (minimum is { } min && maximum is { } max && min > max)
        {
            throw new ArgumentException($"Minimum {name} cannot exceed maximum {name}.", parameterName);
        }
    }

    private static void Add(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        decimal? value) =>
        RequestQuery.Add(query, name, value?.ToString(CultureInfo.InvariantCulture));

    private static void Add(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        int? value) =>
        RequestQuery.Add(query, name, value?.ToString(CultureInfo.InvariantCulture));

    private static void Add(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        long? value) =>
        RequestQuery.Add(query, name, value?.ToString(CultureInfo.InvariantCulture));

    private static void Add(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        double? value) =>
        RequestQuery.Add(query, name, value?.ToString(CultureInfo.InvariantCulture));

    private static string FormatNumber(decimal value) =>
        value.ToString("G", CultureInfo.InvariantCulture);

    private static int? ToInt(long? value) => value is null ? null : checked((int)value.Value);
}

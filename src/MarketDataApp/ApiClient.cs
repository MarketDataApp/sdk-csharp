using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MarketDataApp.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketDataApp;

internal sealed class ApiClient : IDisposable
{
    // Fixed 99s per-request timeout satisfies §10 and is intentionally not configurable.
    // A separate 2s connection timeout is the caller-owned HttpClient handler's concern.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(99);

    private readonly HttpClient _httpClient;
    private readonly MarketDataClientOptions _options;
    private readonly ILogger? _logger;
    private readonly LogLevel _minimumLogLevel;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly StatusGate _statusGate;
    private readonly Func<double> _nextJitter;
    private RateLimitSnapshot? _latestRateLimit;

    public ApiClient(HttpClient httpClient, MarketDataClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = _options.Logger;
        _minimumLogLevel = _options.MinimumLogLevel ?? LogLevel.Information;
        if (_options.BaseAddress is null)
        {
            throw new ArgumentException("BaseAddress is required.", nameof(options));
        }
        if (_options.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetries cannot be negative.");
        }
        if (_options.RetryBaseDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RetryBaseDelay cannot be negative.");
        }
        if (_options.RetryMaxDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RetryMaxDelay cannot be negative.");
        }
        if (_options.RetryBaseDelay > _options.RetryMaxDelay)
        {
            throw new ArgumentException("RetryBaseDelay cannot exceed RetryMaxDelay.", nameof(options));
        }
        if (_options.MaxRetryAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetryAfter must be positive.");
        }
        if (_options.RetryJitterFactor is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "RetryJitterFactor must be between 0 and 1.");
        }
        if (_options.MaxConcurrentRequests <= 0 || _options.MaxConcurrentRequests > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxConcurrentRequests must be between 1 and 50.");
        }
        if (_options.TimeProvider is null)
        {
            throw new ArgumentException("TimeProvider is required.", nameof(options));
        }

        ValidateBaseAddress(_options.BaseAddress, nameof(options));
        ValidateApiVersion(_options.ApiVersion, nameof(options));
        ValidateApiToken(_options.ApiToken, nameof(options));
        ValidateUserAgent(_options.UserAgent, nameof(options));
        _concurrencyGate = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
        // Retry backoff jitter sampler. Defaults to Random.Shared (production behavior); tests
        // inject a deterministic sampler via MarketDataClientOptions.RetryJitterSource so the
        // jitter and clamp branches of RetryDelay are exercised deterministically.
        _nextJitter = _options.RetryJitterSource ?? Random.Shared.NextDouble;
        _statusGate = new StatusGate(
            _options.TimeProvider,
            _options.ApiVersion,
            RefreshServiceStatusAsync,
            _logger,
            _minimumLogLevel);
        LogAt(LogLevel.Information)?.LogInformation(
            "Market Data client initialized with base URL {BaseAddress} and API version {ApiVersion}.",
            _options.BaseAddress,
            _options.ApiVersion);
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            LogAt(LogLevel.Debug)?.LogDebug("API token configured with redacted suffix {TokenSuffix}.", RedactToken(_options.ApiToken));
        }
        else
        {
            LogAt(LogLevel.Warning)?.LogWarning("No API token configured; running in demo mode.");
        }
    }

    /// <summary>
    /// Returns <see cref="_logger"/> only when <paramref name="level"/> meets the configured
    /// <see cref="MarketDataClientOptions.MinimumLogLevel"/> (MARKETDATA_LOGGING_LEVEL) threshold,
    /// so SDK diagnostics below the threshold are never emitted. The default Information threshold
    /// suppresses the Debug request/response logs unless DEBUG is configured. Returns
    /// <see langword="null"/> when no logger is attached or the level is below the threshold, so the
    /// call site's null-conditional log call is skipped entirely.
    /// </summary>
    private ILogger? LogAt(LogLevel level) =>
        _logger is not null && level >= _minimumLogLevel ? _logger : null;

    /// <summary>
    /// Merges the client-level request defaults (from env / <see cref="MarketDataClientOptions"/>)
    /// with a per-request <paramref name="options"/> using per-field precedence: a field set on
    /// <paramref name="options"/> wins; otherwise the client-level default applies (§4 configuration
    /// cascade: env / client defaults → per-method params, method wins). Endpoint methods compute
    /// the effective options once and use them for both the query build and any endpoint-specific
    /// reads (Columns, Headers, etc.). <see cref="MarketDataRequestOptions.Limit"/> and
    /// <see cref="MarketDataRequestOptions.Offset"/> have no client-level default and pass through.
    /// </summary>
    internal MarketDataRequestOptions ApplyDefaults(MarketDataRequestOptions? options) =>
        new()
        {
            DateFormat = options?.DateFormat ?? _options.DefaultDateFormat,
            Mode = options?.Mode ?? _options.DefaultMode,
            Limit = options?.Limit,
            Offset = options?.Offset,
            Columns = options?.Columns ?? _options.DefaultColumns,
            Headers = options?.Headers ?? _options.DefaultAddHeaders,
            Human = options?.Human ?? _options.DefaultHuman,
        };

    public RateLimitSnapshot? LatestRateLimit => Volatile.Read(ref _latestRateLimit);

    /// <summary>Releases the concurrency gate owned by this client. The caller-owned
    /// <see cref="HttpClient"/> is intentionally not disposed.</summary>
    public void Dispose() => _concurrencyGate.Dispose();

    internal Uri CreateRequestUri(
        string path,
        bool versioned,
        IEnumerable<KeyValuePair<string, string?>> query) =>
        BuildUri(path, versioned, query);

    public async Task<InternalApiResponse> GetAsync(
        string path,
        bool versioned,
        IEnumerable<KeyValuePair<string, string?>> query,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildUri(path, versioned, query);
        ThrowIfRateLimited(requestUri);
        var retryCount = 0;
        while (true)
        {
            try
            {
                LogAt(LogLevel.Debug)?.LogDebug("Sending GET request to {RequestUrl}.", SafeUri(requestUri));
                return await SendOnceAsync(requestUri, cancellationToken).ConfigureAwait(false);
            }
            catch (MarketDataException exception) when (
                retryCount < _options.MaxRetries && IsRetryable(exception))
            {
                // §9.5: before retrying a retryable server error, consult the cached /status/ gate.
                // Only a "definitely offline" reading blocks the retry; online/unknown proceed. The
                // gate never blocks — it may trigger a non-blocking refresh but is not awaited here.
                // Network errors are never status-gated and keep retrying.
                if (exception is ServerException
                    && _statusGate.EvaluateForRetry(requestUri) == ServiceAvailability.Offline)
                {
                    LogAt(LogLevel.Warning)?.LogWarning(
                        "Skipping retry for {RequestUrl}: the cached /status/ reports the service offline.",
                        SafeUri(requestUri));
                    throw;
                }

                var delay = RetryDelay(exception, retryCount);
                retryCount++;
                using var activity = MarketDataDiagnostics.ActivitySource.StartActivity(
                    "marketdata.retry",
                    ActivityKind.Internal);
                activity?.SetTag("marketdata.retry.count", retryCount);
                activity?.SetTag("marketdata.retry.delay_ms", delay.TotalMilliseconds);
                activity?.SetTag("error.type", exception.GetType().Name);
                await Task.Delay(delay, _options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (MarketDataException exception)
            {
                LogAt(LogLevel.Error)?.LogError(
                    exception,
                    "Market Data request failed with {ExceptionType} for {RequestUrl}.",
                    exception.ExceptionType,
                    SafeUri(exception.RequestUrl));
                throw;
            }
        }
    }

    private async Task<InternalApiResponse> SendOnceAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SendOnceWithinGateAsync(requestUri, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private async Task<InternalApiResponse> SendOnceWithinGateAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        // Fixed 99s per-request timeout satisfies §10 (not configurable); the injected
        // TimeProvider drives the CTS so tests can control time. A separate 2s connection
        // timeout is the caller-owned HttpClient handler's concern.
        using var timeoutCts = new CancellationTokenSource(RequestTimeout, _options.TimeProvider);
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);
        var requestCancellationToken = requestCts.Token;
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }

        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var activity = MarketDataDiagnostics.ActivitySource.StartActivity(
            "marketdata.http.get",
            ActivityKind.Client);
        activity?.SetTag("http.request.method", "GET");
        activity?.SetTag("url.full", SafeUri(requestUri).AbsoluteUri);

        InternalApiResponse result;
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellationToken).ConfigureAwait(false);

            await using var responseContent = await response.Content.ReadAsStreamAsync(requestCancellationToken)
                .ConfigureAwait(false);
            using var memory = new MemoryStream();
            await responseContent.CopyToAsync(memory, requestCancellationToken).ConfigureAwait(false);
            var body = memory.ToArray();
            var requestId = GetHeader(response, "cf-ray") ?? GetHeader(response, "x-request-id");
            var rateLimit = ParseRateLimit(response.Headers);
            if (rateLimit is not null)
            {
                Volatile.Write(ref _latestRateLimit, rateLimit);
            }

            result = new InternalApiResponse(body, requestUri, (int)response.StatusCode, requestId, rateLimit);
            LogAt(LogLevel.Debug)?.LogDebug(
                "Received HTTP {StatusCode} from {RequestUrl}.",
                (int)response.StatusCode,
                SafeUri(requestUri));
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            activity?.SetTag("marketdata.request_id", requestId);
            // A 2xx or 404 is a usable response (404 == "no data"); anything else is mapped to an
            // exception here. On the usable path control falls through to the shared return below.
            if ((int)response.StatusCode is not (>= 200 and < 300 or 404))
            {
                throw CreateException(response.StatusCode, requestUri, requestId, response.Headers, body);
            }
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "timeout");
            activity?.AddException(exception);
            throw new NetworkException(
                "The Market Data API request timed out.",
                ErrorContext.ForNoResponse(requestUri, _options.TimeProvider.GetUtcNow()),
                exception);
        }
        catch (HttpRequestException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw new NetworkException(
                "The Market Data API request could not be sent.",
                ErrorContext.ForNoResponse(requestUri, _options.TimeProvider.GetUtcNow()),
                exception);
        }

        return result;
    }

    /// <summary>
    /// Records a <c>/status/</c> reading captured by an explicit <c>GetStatusAsync</c> so that the
    /// retry gate and the caller share a single cache (§9.5).
    /// </summary>
    internal void RecordStatus(IReadOnlyList<Utilities.ServiceStatus> services) =>
        _statusGate.Record(services);

    /// <summary>
    /// Fetches <c>/status/</c> for the background refresh. It performs a single send that bypasses
    /// the retry loop, the status gate, and the pre-flight rate-limit check (avoiding recursion),
    /// so its failures are simply swallowed by the caller and leave the status unknown.
    /// </summary>
    private async Task<IReadOnlyList<Utilities.ServiceStatus>> RefreshServiceStatusAsync(
        CancellationToken cancellationToken)
    {
        var requestUri = BuildUri("status", versioned: false, Array.Empty<KeyValuePair<string, string?>>());
        var response = await SendOnceAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return UtilitiesApi.ParseServiceStatuses(response);
    }

    private static bool IsRetryable(MarketDataException exception) =>
        exception is NetworkException
        || exception.StatusCode is >= 501 and <= 599;

    // Computes the delay before the next retry attempt. A retryable exception is either a
    // NetworkException or a 501-599 ServerException (see IsRetryable); a 429 RateLimitException is
    // never retryable, so no RateLimitException arm is needed here. A server-supplied Retry-After
    // (ServerException) is honored and capped at MaxRetryAfter; otherwise an exponential backoff
    // with optional jitter is used, bounded by RetryMaxDelay. Internal so the jitter and overflow
    // branches can be exercised directly by unit tests.
    internal TimeSpan RetryDelay(MarketDataException exception, int retryCount)
    {
        var retryAfter = exception switch
        {
            ServerException server => server.RetryAfter,
            _ => null
        };
        if (retryAfter is { } serverDelay)
        {
            return serverDelay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : TimeSpan.FromTicks(Math.Min(serverDelay.Ticks, _options.MaxRetryAfter.Ticks));
        }

        var multiplier = 1L << Math.Min(retryCount, 30);
        var ticks = Math.Min(
            _options.RetryMaxDelay.Ticks,
            _options.RetryBaseDelay.Ticks > long.MaxValue / multiplier
                ? long.MaxValue
                : _options.RetryBaseDelay.Ticks * multiplier);
        if (ticks == 0 || _options.RetryJitterFactor == 0)
        {
            return TimeSpan.FromTicks(ticks);
        }

        var jitter = 1 - _options.RetryJitterFactor
            + (_nextJitter() * 2 * _options.RetryJitterFactor);
        var jitteredTicks = ticks * jitter;
        var boundedTicks = jitteredTicks >= _options.RetryMaxDelay.Ticks
            ? _options.RetryMaxDelay.Ticks
            : (long)jitteredTicks;
        return TimeSpan.FromTicks(boundedTicks);
    }

    private Uri BuildUri(
        string path,
        bool versioned,
        IEnumerable<KeyValuePair<string, string?>> query)
    {
        var baseUri = _options.BaseAddress.AbsoluteUri.TrimEnd('/') + "/";
        var relativePath = versioned
            ? $"{_options.ApiVersion.Trim('/')}/{path.Trim('/')}/"
            : $"{path.Trim('/')}/";
        var builder = new UriBuilder(new Uri(new Uri(baseUri), relativePath));
        var queryString = query
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}");
        builder.Query = string.Join("&", queryString);
        return builder.Uri;
    }

    // A 404 is handled as a successful no-data response upstream (SendOnceWithinGateAsync) and never
    // reaches this mapper, so there is no NotFound arm; NotFoundException remains part of the taxonomy.
    private MarketDataException CreateException(
        HttpStatusCode statusCode,
        Uri requestUri,
        string? requestId,
        HttpResponseHeaders headers,
        byte[] body)
    {
        var context = ErrorContext.ForResponse(
            requestId,
            requestUri,
            (int)statusCode,
            _options.TimeProvider.GetUtcNow());
        var detail = Encoding.UTF8.GetString(body);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"The Market Data API returned HTTP {(int)statusCode}."
            : $"The Market Data API returned HTTP {(int)statusCode}: {detail}";
        return statusCode switch
        {
            HttpStatusCode.BadRequest => new BadRequestException(message, context),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AuthenticationException(message, context),
            HttpStatusCode.TooManyRequests => new RateLimitException(message, context, ParseRetryAfter(headers)),
            >= HttpStatusCode.InternalServerError => new ServerException(message, context, ParseRetryAfter(headers)),
            _ => new MarketDataExceptionAdapter(message, context)
        };
    }

    private TimeSpan? ParseRetryAfter(HttpResponseHeaders headers)
    {
        if (headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (headers.RetryAfter?.Date is { } date)
        {
            return date - _options.TimeProvider.GetUtcNow();
        }

        return null;
    }

    private static RateLimitSnapshot? ParseRateLimit(HttpResponseHeaders headers)
    {
        if (!TryReadLong(headers, "x-api-ratelimit-limit", out var limit)
            || !TryReadLong(headers, "x-api-ratelimit-remaining", out var remaining)
            || !TryReadLong(headers, "x-api-ratelimit-reset", out var reset)
            || !TryReadLong(headers, "x-api-ratelimit-consumed", out var consumed))
        {
            return null;
        }

        return new RateLimitSnapshot(
            checked((int)limit),
            checked((int)remaining),
            DateTimeOffset.FromUnixTimeSeconds(reset),
            checked((int)consumed));
    }

    private static bool TryReadLong(HttpHeaders headers, string name, out long value)
    {
        return long.TryParse(GetHeader(headers, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string? GetHeader(HttpResponseMessage response, string name) =>
        GetHeader(response.Headers, name) ?? GetHeader(response.Content.Headers, name);

    private static string? GetHeader(HttpHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private void ThrowIfRateLimited(Uri requestUri)
    {
        var snapshot = LatestRateLimit;
        if (snapshot is null
            || snapshot.Limit == 0
            || snapshot.Remaining > 0
            || snapshot.Reset <= _options.TimeProvider.GetUtcNow())
        {
            return;
        }

        var retryAfter = snapshot.Reset - _options.TimeProvider.GetUtcNow();
        throw new RateLimitException(
            "The request was not sent because the latest rate-limit snapshot is exhausted.",
            ErrorContext.ForNoResponse(requestUri, _options.TimeProvider.GetUtcNow()),
            retryAfter);
    }

    private static void ValidateBaseAddress(Uri baseAddress, string parameterName)
    {
        if (!baseAddress.IsAbsoluteUri
            || baseAddress.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(baseAddress.Host)
            || !string.IsNullOrEmpty(baseAddress.Query)
            || !string.IsNullOrEmpty(baseAddress.Fragment)
            || !string.IsNullOrEmpty(baseAddress.UserInfo))
        {
            throw new ArgumentException(
                "BaseAddress must be an absolute HTTP(S) URI with no query, fragment, or user information.",
                parameterName);
        }
    }

    private static void ValidateApiVersion(string apiVersion, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(apiVersion)
            || apiVersion.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-')))
        {
            throw new ArgumentException(
                "ApiVersion may contain only ASCII letters, digits, periods, underscores, and hyphens.",
                parameterName);
        }
    }

    private static void ValidateApiToken(string? apiToken, string parameterName)
    {
        if (apiToken is not null
            && apiToken.Any(character => character is < ' ' or > '~'))
        {
            throw new ArgumentException(
                "ApiToken may contain only printable ASCII characters.",
                parameterName);
        }
    }

    private static void ValidateUserAgent(string userAgent, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            throw new ArgumentException("UserAgent cannot be blank.", parameterName);
        }

        using var request = new HttpRequestMessage();
        try
        {
            request.Headers.UserAgent.ParseAdd(userAgent);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("UserAgent is not a valid HTTP user-agent value.", parameterName, exception);
        }
    }

    private static Uri SafeUri(Uri requestUri) =>
        new UriBuilder(requestUri) { Query = string.Empty, Fragment = string.Empty }.Uri;

    /// <summary>
    /// Performs an asynchronous <c>GET /user/</c> to fail fast on an invalid token
    /// (throwing <see cref="AuthenticationException"/>) and to seed
    /// <see cref="LatestRateLimit"/> from the response headers. Invoked by
    /// <see cref="MarketDataClient.CreateAsync"/>; never called from a constructor.
    /// </summary>
    internal async Task ValidateTokenAndSeedRateLimitAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await GetAsync(
                "user",
                versioned: false,
                Array.Empty<KeyValuePair<string, string?>>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (MarketDataException exception)
        {
            LogAt(LogLevel.Error)?.LogError(
                exception,
                "Startup token validation failed with {ExceptionType} for {RequestUrl}.",
                exception.ExceptionType,
                SafeUri(exception.RequestUrl));
            throw;
        }
    }

    private static string RedactToken(string token) =>
        token.Length <= 4 ? "****" : $"****{token[^4..]}";

    private sealed class MarketDataExceptionAdapter(string message, ErrorContext context)
        : MarketDataException(message, context);
}

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
    private readonly SemaphoreSlim _concurrencyGate;
    private RateLimitSnapshot? _latestRateLimit;

    public ApiClient(HttpClient httpClient, MarketDataClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = _options.Logger;
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
        _logger?.LogInformation(
            "Market Data client initialized with base URL {BaseAddress} and API version {ApiVersion}.",
            _options.BaseAddress,
            _options.ApiVersion);
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _logger?.LogDebug("API token configured with redacted suffix {TokenSuffix}.", RedactToken(_options.ApiToken));
            if (_options.ValidateTokenOnStartup)
            {
                ValidateTokenOnStartup();
            }
        }
        else
        {
            _logger?.LogWarning("No API token configured; running in demo mode.");
        }
    }

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
                _logger?.LogDebug("Sending GET request to {RequestUrl}.", SafeUri(requestUri));
                return await SendOnceAsync(requestUri, cancellationToken).ConfigureAwait(false);
            }
            catch (MarketDataException exception) when (
                retryCount < _options.MaxRetries && IsRetryable(exception))
            {
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
                _logger?.LogError(
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

            var result = new InternalApiResponse(body, requestUri, (int)response.StatusCode, requestId, rateLimit);
            _logger?.LogDebug(
                "Received HTTP {StatusCode} from {RequestUrl}.",
                (int)response.StatusCode,
                SafeUri(requestUri));
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            activity?.SetTag("marketdata.request_id", requestId);
            if ((int)response.StatusCode is >= 200 and < 300 or 404)
            {
                return result;
            }

            throw CreateException(response.StatusCode, requestUri, requestId, response.Headers, body);
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
    }

    private static bool IsRetryable(MarketDataException exception) =>
        exception is NetworkException
        || exception.StatusCode is >= 501 and <= 599;

    private TimeSpan RetryDelay(MarketDataException exception, int retryCount)
    {
        var retryAfter = exception switch
        {
            RateLimitException rateLimit => rateLimit.RetryAfter,
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
            + (Random.Shared.NextDouble() * 2 * _options.RetryJitterFactor);
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
            HttpStatusCode.NotFound => new NotFoundException(message, context),
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

    private void ValidateTokenOnStartup()
    {
        try
        {
            _ = GetAsync(
                "user",
                versioned: false,
                Array.Empty<KeyValuePair<string, string?>>(),
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (MarketDataException exception)
        {
            _logger?.LogError(
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

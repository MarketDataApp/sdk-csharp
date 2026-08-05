using MarketDataApp.Utilities;
using Microsoft.Extensions.Logging;

namespace MarketDataApp;

/// <summary>
/// Availability decision returned by <see cref="StatusGate"/> for a request that is about to be
/// retried after a server error. <see cref="Offline"/> is the only value that blocks a retry.
/// </summary>
internal enum ServiceAvailability
{
    /// <summary>The service is reported online, or no definite information is available.</summary>
    Online,
    /// <summary>The service is definitely reported offline; a retry should be skipped.</summary>
    Offline,
    /// <summary>Status is unknown (cache empty/stale, or the service could not be mapped).</summary>
    Unknown
}

/// <summary>
/// Thread-safe cache of the most recent <c>/status/</c> reading, consulted before retrying a
/// retryable server error (§9.5). The cache ages entries with an injected
/// <see cref="TimeProvider"/> using two thresholds:
/// <list type="bullet">
/// <item>age &lt; <see cref="RefreshInterval"/> (270s): the cached status is used and no refresh is triggered.</item>
/// <item><see cref="RefreshInterval"/> ≤ age &lt; <see cref="CacheValidity"/> (300s): the cached status is
/// used <em>and</em> a non-blocking refresh is triggered.</item>
/// <item>age ≥ <see cref="CacheValidity"/> or the cache is empty: a non-blocking refresh is triggered and
/// the status is treated as <see cref="ServiceAvailability.Unknown"/>.</item>
/// </list>
/// Refreshes are fire-and-forget, never overlap, and swallow their errors, so a failed refresh
/// simply leaves the status unknown.
/// </summary>
internal sealed class StatusGate
{
    /// <summary>Age beyond which a cached reading triggers a background refresh.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(270);

    /// <summary>Age beyond which a cached reading is no longer usable.</summary>
    private static readonly TimeSpan CacheValidity = TimeSpan.FromSeconds(300);

    private readonly TimeProvider _timeProvider;
    private readonly string _apiVersion;
    private readonly Func<CancellationToken, Task<IReadOnlyList<ServiceStatus>>> _refreshAsync;
    private readonly ILogger? _logger;
    private readonly LogLevel _minimumLogLevel;
    private readonly object _gate = new();

    private Snapshot? _snapshot;
    private Task? _refreshInFlight;

    public StatusGate(
        TimeProvider timeProvider,
        string apiVersion,
        Func<CancellationToken, Task<IReadOnlyList<ServiceStatus>>> refreshAsync,
        ILogger? logger,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        _timeProvider = timeProvider;
        _apiVersion = apiVersion;
        _refreshAsync = refreshAsync;
        _logger = logger;
        _minimumLogLevel = minimumLogLevel;
    }

    /// <summary>
    /// Records a fresh <c>/status/</c> reading, stamping it with the current time. Called both by
    /// the background refresh and by an explicit <c>GetStatusAsync</c>, so the two share one cache.
    /// </summary>
    public void Record(IReadOnlyList<ServiceStatus> services)
    {
        var capturedAt = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            _snapshot = new Snapshot(services, capturedAt);
        }
    }

    /// <summary>
    /// Decides whether the service targeted by <paramref name="requestUri"/> may be retried, and
    /// triggers a non-blocking refresh when the cache is stale or empty. This method never blocks
    /// on the refresh; the returned value reflects only the currently cached reading.
    /// </summary>
    public ServiceAvailability EvaluateForRetry(Uri requestUri)
    {
        Snapshot? snapshot;
        bool useCached;
        lock (_gate)
        {
            snapshot = _snapshot;
            var age = snapshot is null
                ? TimeSpan.MaxValue
                : _timeProvider.GetUtcNow() - snapshot.CapturedAt;

            if (snapshot is null || age >= CacheValidity)
            {
                // Empty or expired: refresh and treat the current status as unknown.
                useCached = false;
                TriggerRefreshLocked();
            }
            else if (age >= RefreshInterval)
            {
                // Still valid but due for refresh: use the cached reading and refresh in the background.
                useCached = true;
                TriggerRefreshLocked();
            }
            else
            {
                // Fresh: use the cached reading with no refresh.
                useCached = true;
            }
        }

        // useCached is set true only in the branches above where snapshot is non-null, so the
        // null-forgiving access is safe and a separate null check here would be unreachable.
        return useCached
            ? Evaluate(snapshot!.Services, requestUri)
            : ServiceAvailability.Unknown;
    }

    // Starts a fire-and-forget refresh unless one is already running. Must be called under _gate.
    // The refresh body is offloaded to the thread pool so no HTTP work runs under the lock.
    private void TriggerRefreshLocked()
    {
        if (_refreshInFlight is { IsCompleted: false })
        {
            return;
        }

        _refreshInFlight = Task.Run(RunRefreshAsync);
    }

    private async Task RunRefreshAsync()
    {
        try
        {
            var services = await _refreshAsync(CancellationToken.None).ConfigureAwait(false);
            Record(services);
        }
        catch (Exception exception)
        {
            // A failed refresh must never surface; it just leaves the status unknown. Gated by the
            // configured MARKETDATA_LOGGING_LEVEL threshold so this Debug diagnostic is suppressed
            // unless DEBUG is configured.
            if (_logger is not null && LogLevel.Debug >= _minimumLogLevel)
            {
                _logger.LogDebug(
                    exception,
                    "Background /status/ refresh failed; service status left unknown.");
            }
        }
    }

    // Maps the request to a service key and aggregates the matching status rows. Only when
    // matching rows exist and none is online is the service treated as definitely offline.
    private ServiceAvailability Evaluate(IReadOnlyList<ServiceStatus> services, Uri requestUri)
    {
        var key = ServiceKey(requestUri.AbsolutePath);
        if (key is null)
        {
            return ServiceAvailability.Unknown;
        }

        var anyMatch = false;
        var anyOnline = false;
        foreach (var service in services)
        {
            if (!string.Equals(ServiceKey(service.Service), key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            anyMatch = true;
            if (service.Online)
            {
                anyOnline = true;
            }
        }

        if (!anyMatch)
        {
            return ServiceAvailability.Unknown;
        }

        return anyOnline ? ServiceAvailability.Online : ServiceAvailability.Offline;
    }

    // Extracts the service segment (e.g. "stocks") from a path such as "/v1/stocks/quotes/AAPL/"
    // or a status service value such as "/v1/stocks/quotes/", skipping the version prefix.
    private string? ServiceKey(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var index = string.Equals(segments[0], _apiVersion, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
        return index < segments.Length ? segments[index] : null;
    }

    private sealed record Snapshot(IReadOnlyList<ServiceStatus> Services, DateTimeOffset CapturedAt);
}

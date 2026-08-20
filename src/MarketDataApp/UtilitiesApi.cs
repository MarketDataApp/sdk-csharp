using System.Text.Json;
using MarketDataApp.Exceptions;
using MarketDataApp.Utilities;

namespace MarketDataApp;

/// <summary>Asynchronous utility endpoints.</summary>
public sealed class UtilitiesApi
{
    private readonly ApiClient _apiClient;

    internal UtilitiesApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets the operational status of every Market Data API service.</summary>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>One <c>ServiceStatus</c> per service (name, online flag, 30/90-day uptime); the reading also feeds the SDK's retry gate.</returns>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Utilities.GetStatusAsync();
    /// foreach (var service in response.Values)
    ///     Console.WriteLine($"{service.Service}: {service.Status}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/utilities/status/"/>
    public async Task<UtilitiesStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            "status",
            versioned: false,
            Array.Empty<KeyValuePair<string, string?>>(),
            cancellationToken).ConfigureAwait(false);
        var values = ParseServiceStatuses(response);
        // Feed the retry gate (§9.5) so an explicit status check and the background refresh
        // share a single cache.
        _apiClient.RecordStatus(values);
        return JsonResponseParser.CreateResponse<UtilitiesStatusResponse, IReadOnlyList<ServiceStatus>>(response, values);
    }

    /// <summary>
    /// Parses a <c>/status/</c> response into per-service readings. Shared by
    /// <see cref="GetStatusAsync"/> and the retry gate's background refresh so both decode the
    /// response identically.
    /// </summary>
    internal static IReadOnlyList<ServiceStatus> ParseServiceStatuses(InternalApiResponse response) =>
        JsonResponseParser.DecodeOrDefault(
            response,
            root => JsonResponseParser.ReadParallelArray(
                root,
                row => new ServiceStatus(
                    row.String("service") ?? throw new JsonException("Missing service."),
                    row.String("status") ?? throw new JsonException("Missing status."),
                    row.Boolean("online") ?? throw new JsonException("Missing online."),
                    row.Double("uptimePct30d") ?? throw new JsonException("Missing uptimePct30d."),
                    row.Double("uptimePct90d") ?? throw new JsonException("Missing uptimePct90d."),
                    row.Timestamp("updated") ?? throw new JsonException("Missing updated.")),
                "service", "status", "online", "uptimePct30d", "uptimePct90d", "updated"),
            Array.Empty<ServiceStatus>(),
            requireStatus: true);

    /// <summary>Gets the request headers exactly as the API observed them, for debugging proxies and middleware.</summary>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The observed headers as a case-insensitive name/value dictionary.</returns>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Utilities.GetHeadersAsync();
    /// foreach (var (name, value) in response.Values)
    ///     Console.WriteLine($"{name}: {value}");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/utilities/headers/"/>
    public async Task<UtilitiesHeadersResponse> GetHeadersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            "headers",
            versioned: false,
            Array.Empty<KeyValuePair<string, string?>>(),
            cancellationToken).ConfigureAwait(false);
        var values = JsonResponseParser.DecodeOrDefault(
            response,
            root => root.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            requireStatus: false);
        return JsonResponseParser.CreateResponse<UtilitiesHeadersResponse, IReadOnlyDictionary<string, string>>(response, values);
    }

    /// <summary>Gets the authenticated user's quota and entitlement information.</summary>
    /// <param name="cancellationToken">Cancels the in-flight request.</param>
    /// <returns>The user's quota counters and options-data permission; the canonical live values come from the <c>x-api-ratelimit-*</c> headers surfaced on <see cref="MarketDataClient.LatestRateLimit"/>.</returns>
    /// <exception cref="BadRequestException">The API rejected the request as malformed (HTTP 400).</exception>
    /// <exception cref="AuthenticationException">The token is missing, invalid, or not entitled to the requested data (HTTP 401 or 403).</exception>
    /// <exception cref="RateLimitException">The plan's request quota is exhausted (HTTP 429, or preemptively from the tracked snapshot); <see cref="RateLimitException.RetryAfter"/> indicates how long to wait.</exception>
    /// <exception cref="ServerException">The API kept returning a 5xx error after the automatic retries.</exception>
    /// <exception cref="NetworkException">The request could not be sent, timed out, or was canceled by the HttpClient's configured timeout.</exception>
    /// <exception cref="ParseException">The success response did not match the documented shape.</exception>
    /// <example><code>
    /// var response = await client.Utilities.GetUserAsync();
    /// Console.WriteLine($"{response.Values.RequestsRemaining}/{response.Values.RequestsLimit} requests left");
    /// </code></example>
    /// <seealso href="https://www.marketdata.app/docs/sdk/csharp/utilities/user/"/>
    public async Task<UtilitiesUserResponse> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            "user",
            versioned: false,
            Array.Empty<KeyValuePair<string, string?>>(),
            cancellationToken).ConfigureAwait(false);
        var user = JsonResponseParser.Decode(
            response,
            root => new User(
                RequiredInt(root, "x-ratelimit-requests-remaining"),
                RequiredInt(root, "x-ratelimit-requests-limit"),
                RequiredString(root, "x-options-data-permissions")),
            requireStatus: false);
        return JsonResponseParser.CreateResponse<UtilitiesUserResponse, User>(response, user);
    }

    private static int RequiredInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new JsonException($"Missing or invalid {name}.");

    private static string RequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"Missing or invalid {name}.");
}

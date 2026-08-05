using System.Text.Json;
using MarketDataApp.Utilities;

namespace MarketDataApp;

/// <summary>Asynchronous utility endpoints.</summary>
public sealed class UtilitiesApi
{
    private readonly ApiClient _apiClient;

    internal UtilitiesApi(ApiClient apiClient) => _apiClient = apiClient;

    /// <summary>Gets the operational status of API services.</summary>
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

    /// <summary>Gets the request headers observed by the API.</summary>
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

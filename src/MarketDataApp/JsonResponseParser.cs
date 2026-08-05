using System.Globalization;
using System.Text;
using System.Text.Json;
using MarketDataApp.Exceptions;

namespace MarketDataApp;

internal static class JsonResponseParser
{
    private static readonly Lazy<TimeZoneInfo> EasternTimeZone = new(ResolveEasternTimeZone);

    // Excluded: the "Eastern Standard Time" fallback is only reached on platforms that lack the
    // IANA "America/New_York" id (Windows). On Linux/macOS/CI — where the tests run — the primary
    // lookup always succeeds, so the catch/fallback branch is unreachable there.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }

    public static JsonElement Parse(InternalApiResponse response)
    {
        try
        {
            using var document = JsonDocument.Parse(response.Body);
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            throw new ParseException(
                "The Market Data API returned invalid JSON.",
                ErrorContext.ForResponse(response.RequestId, response.RequestUrl, response.StatusCode, DateTimeOffset.UtcNow),
                exception);
        }
    }

    public static IReadOnlyList<T> ReadParallelArray<T>(
        JsonElement root,
        Func<ParallelArrayRow, T> factory,
        params string[] fields)
    {
        var lengths = new List<int>(fields.Length);
        foreach (var field in fields)
        {
            if (!root.TryGetProperty(field, out var value))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException($"Response field '{field}' must be an array.");
            }

            lengths.Add(value.GetArrayLength());
        }

        if (lengths.Distinct().Skip(1).Any())
        {
            throw new JsonException("Parallel response arrays have different lengths.");
        }

        var count = lengths.Count == 0 ? 0 : lengths[0];

        var rows = new List<T>(count);
        for (var index = 0; index < count; index++)
        {
            rows.Add(factory(new ParallelArrayRow(root, index)));
        }

        return rows;
    }

    public readonly struct ParallelArrayRow(JsonElement root, int index)
    {
        public string? String(string name) => Value(name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString()
            : null;

        public double? Double(string name) => Value(name) is { } value && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var result) ? result : null;

        public decimal? Decimal(string name) => Value(name) is { } value && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var result) ? result : null;

        public long? Long(string name) => Value(name) is { } value && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var result) ? result : null;

        public bool? Boolean(string name) => Value(name) is { } value && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

        public DateTimeOffset? Timestamp(string name) => ToDateTime(Value(name));

        private JsonElement? Value(string name)
        {
            if (!root.TryGetProperty(name, out var values)
                || values.ValueKind != JsonValueKind.Array
                || index >= values.GetArrayLength())
            {
                return null;
            }

            var value = values[index];
            return value.ValueKind == JsonValueKind.Null ? null : value;
        }
    }

    public static DateTimeOffset? ToDateTime(JsonElement? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number
            && value.Value.TryGetDouble(out var number))
        {
            return ToEastern(DateTimeOffset.UnixEpoch.AddSeconds(number));
        }

        if (value.Value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                value.Value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return ToEastern(timestamp);
        }

        return null;
    }

    private static DateTimeOffset ToEastern(DateTimeOffset timestamp) =>
        TimeZoneInfo.ConvertTime(timestamp, EasternTimeZone.Value);

    public static DateTimeOffset? Timestamp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? ToDateTime(value) : null;

    public static T Decode<T>(
        InternalApiResponse response,
        Func<JsonElement, T> decoder,
        bool requireStatus = true,
        IReadOnlyList<string>? requestedColumns = null)
    {
        return Decode(
            response,
            decoder,
            default!,
            useDefaultForNoData: false,
            requireStatus,
            requestedColumns);
    }

    public static T DecodeOrDefault<T>(
        InternalApiResponse response,
        Func<JsonElement, T> decoder,
        T noDataValue,
        bool requireStatus = true,
        IReadOnlyList<string>? requestedColumns = null) =>
        Decode(
            response,
            decoder,
            noDataValue,
            useDefaultForNoData: true,
            requireStatus,
            requestedColumns);

    private static T Decode<T>(
        InternalApiResponse response,
        Func<JsonElement, T> decoder,
        T noDataValue,
        bool useDefaultForNoData,
        bool requireStatus,
        IReadOnlyList<string>? requestedColumns)
    {
        if (useDefaultForNoData && response.StatusCode == 404)
        {
            return noDataValue;
        }

        try
        {
            var root = Parse(response);
            var status = ValidateStatus(root, requireStatus);
            if (useDefaultForNoData && status == "no_data")
            {
                return noDataValue;
            }

            ValidateRequestedColumns(root, requestedColumns);
            return decoder(root);
        }
        catch (Exception exception) when (IsParseFailure(exception))
        {
            throw new ParseException(
                "The Market Data API response did not match the expected shape.",
                ErrorContext.ForResponse(response.RequestId, response.RequestUrl, response.StatusCode, DateTimeOffset.UtcNow),
                exception);
        }

    }

    private static bool IsParseFailure(Exception exception) =>
        exception is JsonException
        or InvalidOperationException
        or FormatException
        or OverflowException
        or ArgumentOutOfRangeException;

    private static string? ValidateStatus(JsonElement root, bool requireStatus)
    {
        if (!root.TryGetProperty("s", out var status))
        {
            if (requireStatus)
            {
                throw new JsonException("Missing response status field 's'.");
            }

            return null;
        }

        if (status.ValueKind != JsonValueKind.String
            || status.GetString() is not ("ok" or "no_data"))
        {
            throw new JsonException(
                $"Unexpected response status '{(status.ValueKind == JsonValueKind.String ? status.GetString() : status.ValueKind.ToString())}'.");
        }

        return status.GetString();
    }

    private static void ValidateRequestedColumns(
        JsonElement root,
        IReadOnlyList<string>? requestedColumns)
    {
        if (requestedColumns is null)
        {
            return;
        }

        foreach (var column in requestedColumns)
        {
            if (!root.TryGetProperty(column, out _))
            {
                throw new JsonException($"Requested response column '{column}' is missing.");
            }
        }
    }

    public static TResponse CreateResponse<TResponse, T>(
        InternalApiResponse response,
        T values,
        Func<TResponse, TResponse>? customize = null)
        where TResponse : MarketDataResponse<T>, new()
    {
        var result = new TResponse
        {
            Values = values,
            StatusCode = response.StatusCode,
            RequestUrl = response.RequestUrl,
            RequestId = response.RequestId,
            RateLimit = response.RateLimit,
            RawBodyBytes = response.Body,
            IsNoData = IsNoData(response),
            Parts =
            [
                new MarketDataResponsePart
                {
                    StatusCode = response.StatusCode,
                    RequestUrl = response.RequestUrl,
                    RequestId = response.RequestId,
                    RateLimit = response.RateLimit,
                    RawBody = Encoding.UTF8.GetString(response.Body)
                }
            ]
        };
        return customize is null ? result : customize(result);
    }

    public static CsvResponse CreateCsvResponse(InternalApiResponse response) =>
        CreateResponse<CsvResponse, string>(response, Encoding.UTF8.GetString(response.Body));

    public static TResponse CreateCompositeResponse<TResponse, T>(
        T values,
        Uri logicalRequestUrl,
        int statusCode,
        byte[] body,
        IReadOnlyList<MarketDataResponsePart> parts)
        where TResponse : MarketDataResponse<T>, new() =>
        new()
        {
            Values = values,
            StatusCode = statusCode,
            RequestUrl = logicalRequestUrl,
            RequestId = null,
            RateLimit = null,
            RawBodyBytes = body,
            IsNoData = statusCode == 404,
            Parts = parts
        };

    private static bool IsNoData(InternalApiResponse response)
    {
        if (response.StatusCode == 404)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            return document.RootElement.TryGetProperty("s", out var status)
                && status.ValueKind == JsonValueKind.String
                && status.GetString() == "no_data";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

namespace MarketDataApp.Exceptions;

/// <summary>Diagnostic context attached to every <see cref="MarketDataException"/>.</summary>
public sealed record ErrorContext
{
    /// <summary>Server-assigned request identifier, if present in the response.</summary>
    public string? RequestId { get; init; }

    /// <summary>URL that was requested (query string retained for diagnostics).</summary>
    public required Uri RequestUrl { get; init; }

    /// <summary>HTTP status code, or 0 when no response was received (network error).</summary>
    public int StatusCode { get; init; }

    /// <summary>Timestamp when the exception was created.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Creates context for a response that was received from the server.</summary>
    public static ErrorContext ForResponse(string? requestId, Uri requestUrl, int statusCode, DateTimeOffset timestamp) =>
        new() { RequestId = requestId, RequestUrl = requestUrl, StatusCode = statusCode, Timestamp = ToEastern(timestamp) };

    /// <summary>Creates context for a failure where no HTTP response was received.</summary>
    public static ErrorContext ForNoResponse(Uri requestUrl, DateTimeOffset timestamp) =>
        new() { RequestUrl = requestUrl, StatusCode = 0, Timestamp = ToEastern(timestamp) };

    /// <summary>Expresses an instant in US/Eastern.</summary>
    /// <param name="timestamp">The instant to convert.</param>
    /// <returns>The same instant, carrying the EST or EDT offset in force at that moment.</returns>
    private static DateTimeOffset ToEastern(DateTimeOffset timestamp) =>
        TimeZoneInfo.ConvertTime(timestamp, EasternTime.Zone);
}

using System.Globalization;

namespace MarketDataApp.Exceptions;

/// <summary>
/// Abstract base for all Market Data SDK exceptions. The closed set of derived types is:
/// <see cref="AuthenticationException"/>, <see cref="BadRequestException"/>,
/// <see cref="NetworkException"/>, <see cref="NotFoundException"/>,
/// <see cref="ParseException"/>, <see cref="RateLimitException"/>, <see cref="ServerException"/>.
/// </summary>
public abstract class MarketDataException : Exception
{
    /// <param name="message">Human-readable description of the error.</param>
    /// <param name="context">Diagnostic context from the request/response.</param>
    /// <param name="inner">Underlying cause, if any.</param>
    protected MarketDataException(string message, ErrorContext context, Exception? inner = null)
        : base(message, inner)
    {
        Context = context;
    }

    /// <summary>Diagnostic context for this error.</summary>
    public ErrorContext Context { get; }

    /// <summary>Server-assigned request ID, or <c>null</c> if not available.</summary>
    public string? RequestId => Context.RequestId;

    /// <summary>URL that was requested. Query string is present for diagnostics.</summary>
    public Uri RequestUrl => Context.RequestUrl;

    /// <summary>HTTP status code, or 0 for network errors.</summary>
    public int StatusCode => Context.StatusCode;

    /// <summary>Timestamp when the exception was created.</summary>
    public DateTimeOffset Timestamp => Context.Timestamp;

    /// <summary>Simple class name of this exception type.</summary>
    public string ExceptionType => GetType().Name;

    // Fixed header for the support block (per MarketData.app SDK requirements §6.3).
    // The footer is a rule of dashes the same length as the header.
    private const string SupportInfoHeader = "--- MARKET DATA SUPPORT INFO ---";

    /// <summary>
    /// A formatted diagnostic block containing every support field, in a fixed order and
    /// column-aligned, suitable for pasting into a Market Data support ticket. The
    /// timestamp is rendered from the US/Eastern <see cref="Timestamp"/> as
    /// <c>yyyy-MM-dd HH:mm:ss</c> using the invariant culture.
    /// </summary>
    public string SupportInfo
    {
        get
        {
            // Timestamp is already normalized to US/Eastern by ErrorContext; format only.
            var timestamp = Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            (string Label, string Value)[] fields =
            [
                ("request_id:", string.IsNullOrEmpty(RequestId) ? "(none)" : RequestId),
                ("request_url:", RequestUrl.ToString()),
                ("status_code:", StatusCode.ToString(CultureInfo.InvariantCulture)),
                ("timestamp:", timestamp),
                ("message:", Message),
                ("exception_type:", ExceptionType),
            ];

            // Pad every label to the width of the longest ("exception_type:") so values align.
            var width = fields.Max(f => f.Label.Length);
            var lines = new string[fields.Length + 2];
            lines[0] = SupportInfoHeader;
            for (var i = 0; i < fields.Length; i++)
            {
                lines[i + 1] = $"{fields[i].Label.PadRight(width)} {fields[i].Value}";
            }
            lines[^1] = new string('-', SupportInfoHeader.Length);
            return string.Join('\n', lines);
        }
    }
}

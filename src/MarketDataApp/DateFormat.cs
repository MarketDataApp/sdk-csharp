namespace MarketDataApp;

/// <summary>Date serialization format applied to all date/timestamp fields in a response.</summary>
public enum DateFormat
{
    /// <summary>Unix epoch seconds. The API also uses it for most fields when no format is
    /// requested; option expirations then come as dates.</summary>
    Unix,

    /// <summary>US/Eastern text: a datetime with its offset (<c>2026-09-21 14:46:05 -04:00</c>)
    /// or a date alone (<c>2026-09-21</c>), which typed responses read as midnight US/Eastern.</summary>
    Timestamp,

    /// <summary>Excel/spreadsheet serial date number. Supported by the <c>*CsvAsync</c> methods
    /// only; typed methods reject it because serial dates would be misparsed by their
    /// Unix/ISO timestamp decoding.</summary>
    Spreadsheet,
}

internal static class DateFormatExtensions
{
    internal static string ToWireValue(this DateFormat format) => format switch
    {
        DateFormat.Unix => "unix",
        DateFormat.Timestamp => "timestamp",
        DateFormat.Spreadsheet => "spreadsheet",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Parses a wire value (<c>unix</c>/<c>timestamp</c>/<c>spreadsheet</c>,
    /// case-insensitive) into a <see cref="DateFormat"/>.</summary>
    internal static bool TryParseWireValue(string value, out DateFormat format)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "unix":
                format = DateFormat.Unix;
                return true;
            case "timestamp":
                format = DateFormat.Timestamp;
                return true;
            case "spreadsheet":
                format = DateFormat.Spreadsheet;
                return true;
            default:
                format = default;
                return false;
        }
    }
}

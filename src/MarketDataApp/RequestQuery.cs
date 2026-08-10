using System.Globalization;

namespace MarketDataApp;

internal static class RequestQuery
{
    public static List<KeyValuePair<string, string?>> From(
        MarketDataRequestOptions? options,
        bool allowSpreadsheet = false)
    {
        RequestValidator.ValidateRequestOptions(options);
        if (!allowSpreadsheet && options?.DateFormat == DateFormat.Spreadsheet)
        {
            throw new ArgumentException(
                "DateFormat.Spreadsheet returns Excel serial date numbers, which typed methods "
                + "would misparse as Unix timestamps. Use the *CsvAsync methods for spreadsheet "
                + "output.",
                nameof(options));
        }

        var query = new List<KeyValuePair<string, string?>>();
        if (options is null)
        {
            return query;
        }

        Add(query, "dateformat", options.DateFormat?.ToWireValue());
        Add(query, "mode", options.Mode?.ToWireValue());
        Add(query, "limit", options.Limit?.ToString(CultureInfo.InvariantCulture));
        Add(query, "offset", options.Offset?.ToString(CultureInfo.InvariantCulture));
        Add(query, "columns", options.Columns is { Count: > 0 } ? string.Join(",", options.Columns) : null);
        return query;
    }

    public static List<KeyValuePair<string, string?>> Csv(MarketDataRequestOptions? options)
    {
        var query = From(options, allowSpreadsheet: true);
        Add(query, "format", "csv");
        Add(query, "headers", options?.Headers?.ToString().ToLowerInvariant());
        Add(query, "human", options?.Human?.ToString().ToLowerInvariant());
        return query;
    }

    public static void Add(
        ICollection<KeyValuePair<string, string?>> query,
        string name,
        string? value)
    {
        if (value is not null)
        {
            query.Add(new KeyValuePair<string, string?>(name, value));
        }
    }

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static void AddDateWindow(
        ICollection<KeyValuePair<string, string?>> query,
        DateOnly? date,
        DateOnly? from,
        DateOnly? to,
        int? countback)
    {
        Add(query, "date", date is { } dateValue ? Date(dateValue) : null);
        Add(query, "from", from is { } fromValue ? Date(fromValue) : null);
        Add(query, "to", to is { } toValue ? Date(toValue) : null);
        Add(query, "countback", countback?.ToString(CultureInfo.InvariantCulture));
    }
}

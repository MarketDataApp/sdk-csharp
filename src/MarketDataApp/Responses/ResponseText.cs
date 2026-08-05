using System.Globalization;

namespace MarketDataApp;

/// <summary>
/// Culture-invariant formatting helpers shared by the one-line <c>ToString()</c> overrides on the
/// SDK's data records. Keeps those summaries concise and free of duplicated null/format handling.
/// </summary>
internal static class ResponseText
{
    private const string Missing = "n/a";

    public static string F(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? Missing;

    public static string F(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? Missing;

    public static string F(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? Missing;

    public static string F(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? Missing;

    public static string F(string? value) =>
        string.IsNullOrEmpty(value) ? Missing : value;

    /// <summary>Formats a nullable timestamp as an invariant <c>yyyy-MM-dd</c> date.</summary>
    public static string D(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? Missing;

    /// <summary>Formats a non-null timestamp as an invariant <c>yyyy-MM-dd</c> date.</summary>
    public static string D(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Trims free text to a single short fragment for use in a one-line summary.</summary>
    public static string Truncate(string? value, int max = 50)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Missing;
        }

        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}

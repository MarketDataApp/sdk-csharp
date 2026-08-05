namespace MarketDataApp;

/// <summary>Data mode controlling whether live, delayed, or cached data is returned.</summary>
public enum Mode
{
    /// <summary>Real-time data (default).</summary>
    Live,

    /// <summary>Data delayed by the exchange-mandated period.</summary>
    Delayed,

    /// <summary>Previously cached data.</summary>
    Cached,
}

internal static class ModeExtensions
{
    internal static string ToWireValue(this Mode mode) => mode switch
    {
        Mode.Live => "live",
        Mode.Delayed => "delayed",
        Mode.Cached => "cached",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>Parses a wire value (<c>live</c>/<c>delayed</c>/<c>cached</c>, case-insensitive)
    /// into a <see cref="Mode"/>.</summary>
    internal static bool TryParseWireValue(string value, out Mode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "live":
                mode = Mode.Live;
                return true;
            case "delayed":
                mode = Mode.Delayed;
                return true;
            case "cached":
                mode = Mode.Cached;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}

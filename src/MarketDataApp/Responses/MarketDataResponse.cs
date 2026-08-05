using System.Collections;
using System.Globalization;
using System.Text;

namespace MarketDataApp;

/// <summary>
/// Typed response returned by every Market Data SDK endpoint.
/// </summary>
/// <typeparam name="T">Type of the decoded data payload.</typeparam>
public abstract record MarketDataResponse<T> : IMarketDataResponse
{
    /// <summary>Decoded data payload.</summary>
    public T Values { get; internal init; } = default!;

    /// <summary>HTTP status code of the response.</summary>
    public int StatusCode { get; internal init; }

    /// <summary>URL that was requested.</summary>
    public Uri RequestUrl { get; internal init; } = null!;

    /// <summary>Server-assigned request identifier, or <c>null</c> when not returned.</summary>
    public string? RequestId { get; internal init; }

    /// <summary>Rate-limit information from this response's headers.</summary>
    public RateLimitSnapshot? RateLimit { get; internal init; }

    /// <summary>
    /// HTTP responses that contributed to this logical response. A normal endpoint response has
    /// one part; an automatically chunked response has one part per HTTP request.
    /// </summary>
    public IReadOnlyList<MarketDataResponsePart> Parts { get; internal init; } = [];

    // Raw response bytes — internal so the transport can populate it; not exposed on the
    // public interface (callers use RawBody or SaveToFile).
    internal byte[] RawBodyBytes { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Whether the API returned no data for the request. When <c>true</c>, <see cref="Values"/>
    /// is an empty collection (or empty string).
    /// </summary>
    public bool IsNoData { get; internal init; }

    /// <summary>Whether this logical response was assembled from multiple HTTP responses.</summary>
    public bool IsComposite => Parts.Count > 1;

    /// <inheritdoc />
    public virtual bool IsJson => true;

    /// <inheritdoc />
    public virtual bool IsCsv => false;

    /// <inheritdoc />
    public virtual bool IsHtml => false;

    /// <summary>The raw response body decoded as a UTF-8 string.</summary>
    public string RawBody => Encoding.UTF8.GetString(RawBodyBytes);

    /// <summary>
    /// Writes the response to <paramref name="path"/>, choosing the representation from the file
    /// extension: <c>.json</c>/<c>.csv</c>/<c>.html</c> write the matching format when this
    /// response can provide it, and any other extension falls back to the raw response body.
    /// </summary>
    /// <returns>The <paramref name="path"/> that was written.</returns>
    public string SaveToFile(string path)
    {
        File.WriteAllBytes(path, SelectBytesForExtension(path));
        return path;
    }

    /// <summary>Asynchronous counterpart to <see cref="SaveToFile"/>.</summary>
    /// <returns>The <paramref name="path"/> that was written.</returns>
    public async Task<string> SaveToFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllBytesAsync(path, SelectBytesForExtension(path), cancellationToken)
            .ConfigureAwait(false);
        return path;
    }

    // Picks the bytes to persist from the file's extension. When the extension names a format this
    // response can provide (its native format), that representation is written; otherwise we fall
    // back to the raw response body — the response-type default.
    private byte[] SelectBytesForExtension(string path) =>
        RepresentationForExtension(Path.GetExtension(path)) ?? RawBodyBytes;

    // Returns this response's bytes for the requested extension, or null when it cannot provide
    // that format (signalling the caller to fall back to the raw body).
    private byte[]? RepresentationForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".json" when IsJson => RawBodyBytes,
            ".csv" when IsCsv => RawBodyBytes,
            ".html" or ".htm" when IsHtml => RawBodyBytes,
            _ => null,
        };

    /// <summary>
    /// A concise, developer-friendly summary — concrete type, item/byte count, HTTP status, and a
    /// no-data marker — rather than the default record dump or the raw payload. Sealed so every
    /// derived response wrapper shares this format instead of synthesizing its own.
    /// </summary>
    public sealed override string ToString()
    {
        var summary = IsCsv || IsHtml
            ? $"{RawBodyBytes.Length.ToString(CultureInfo.InvariantCulture)} bytes"
            : DescribeItemCount();
        var noData = IsNoData ? ", no data" : string.Empty;
        return $"{GetType().Name}: {summary}, HTTP {StatusCode.ToString(CultureInfo.InvariantCulture)}{noData}";
    }

    private string DescribeItemCount()
    {
        var count = Values switch
        {
            null => 0,
            ICollection collection => collection.Count,
            _ => 1,
        };
        return count == 1
            ? "1 item"
            : $"{count.ToString(CultureInfo.InvariantCulture)} items";
    }
}

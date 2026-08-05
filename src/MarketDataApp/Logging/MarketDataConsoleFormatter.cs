using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace MarketDataApp.Logging;

/// <summary>
/// An opt-in <see cref="ConsoleFormatter"/> that renders the SDK's log events in the canonical
/// MarketData.app layout <c>{timestamp} - {logger_name} - {level} - {message}</c> — for example
/// <c>2025-02-21 12:00:00 - marketdata.client - INFO - Making request...</c>.
/// </summary>
/// <remarks>
/// <para>
/// The timestamp is rendered in US/Eastern (<c>America/New_York</c>) time with the invariant-culture
/// pattern <c>yyyy-MM-dd HH:mm:ss</c> (no offset, no milliseconds). The .NET <see cref="LogLevel"/> is
/// mapped to the spec vocabulary: <c>Trace</c>/<c>Debug</c> → <c>DEBUG</c>, <c>Information</c> →
/// <c>INFO</c>, <c>Warning</c> → <c>WARNING</c>, <c>Error</c>/<c>Critical</c> → <c>ERROR</c>. When a log
/// entry carries an <see cref="System.Exception"/> it is appended after the message.
/// </para>
/// <para>
/// This formatter is <b>opt-in</b>; register it with
/// <c>ILoggingBuilder.AddMarketDataCanonicalConsole()</c>. By default the SDK emits structured
/// <see cref="ILogger"/> events and leaves line formatting to the configured provider.
/// </para>
/// </remarks>
public sealed class MarketDataConsoleFormatter : ConsoleFormatter
{
    /// <summary>The name under which this formatter is registered with the console logger.</summary>
    public const string FormatterName = "marketdata";

    private static readonly Lazy<TimeZoneInfo> EasternTimeZone = new(ResolveEasternTimeZone);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataConsoleFormatter"/> class using the
    /// system clock (<see cref="TimeProvider.System"/>) for timestamps. This is the constructor the
    /// dependency-injection container uses when the formatter is registered.
    /// </summary>
    public MarketDataConsoleFormatter()
        : this(TimeProvider.System)
    {
    }

    // Internal constructor so unit tests can inject a deterministic clock (the test assembly has
    // InternalsVisibleTo access) and assert an exact timestamp without depending on wall-clock time.
    internal MarketDataConsoleFormatter(TimeProvider timeProvider)
        : base(FormatterName)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    // Excluded: the "Eastern Standard Time" fallback is only reached on platforms that lack the
    // IANA "America/New_York" id (Windows). On Linux/macOS/CI — where the tests run — the primary
    // lookup always succeeds, so the catch/fallback branch is unreachable there. This mirrors the
    // identical resolver in ErrorContext and JsonResponseParser.
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

    /// <inheritdoc />
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);

        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var exception = logEntry.Exception;
        var hasMessage = !string.IsNullOrEmpty(message);

        // Nothing to render when there is neither a message nor an exception.
        if (!hasMessage && exception is null)
        {
            return;
        }

        var timestamp = TimeZoneInfo
            .ConvertTime(_timeProvider.GetUtcNow(), EasternTimeZone.Value)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        textWriter.Write(timestamp);
        textWriter.Write(" - ");
        textWriter.Write(logEntry.Category);
        textWriter.Write(" - ");
        textWriter.Write(MapLevel(logEntry.LogLevel));
        textWriter.Write(" - ");

        // The message field is the formatted message, with the exception text appended after it
        // (space-separated) when both a message and an exception are present.
        if (hasMessage)
        {
            textWriter.Write(message);
        }

        if (exception is not null)
        {
            if (hasMessage)
            {
                textWriter.Write(' ');
            }

            textWriter.Write(exception.ToString());
        }

        // Use an explicit LF so the canonical line is identical across operating systems.
        textWriter.Write('\n');
    }

    // Maps the .NET LogLevel to the spec log-level vocabulary. LogLevel.None never reaches a
    // formatter (it disables logging), so the default arm covers Error and Critical.
    private static string MapLevel(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARNING",
        _ => "ERROR",
    };
}

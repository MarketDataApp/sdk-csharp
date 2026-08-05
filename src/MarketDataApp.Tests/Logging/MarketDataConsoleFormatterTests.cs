using MarketDataApp.Logging;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataApp.Tests.Logging;

/// <summary>
/// Unit tests for <see cref="MarketDataConsoleFormatter"/>. The formatter is driven directly with a
/// fixed <see cref="TimeProvider"/> and a <see cref="StringWriter"/> so the exact canonical line is
/// asserted without any dependency on wall-clock time or real console output.
/// </summary>
public sealed class MarketDataConsoleFormatterTests
{
    // 2025-02-21 17:00:00Z is 2025-02-21 12:00:00 in US/Eastern (EST, UTC-5) — the example timestamp
    // from the SDK requirements §7.
    private static readonly DateTimeOffset FixedUtc =
        new(2025, 2, 21, 17, 0, 0, TimeSpan.Zero);

    private const string ExpectedTimestamp = "2025-02-21 12:00:00";

    private static string Render(LogLevel level, string category, string message, Exception? exception)
    {
        var formatter = new MarketDataConsoleFormatter(new ManualTimeProvider(FixedUtc));
        var entry = new LogEntry<string>(
            level,
            category,
            new EventId(0),
            message,
            exception,
            static (state, _) => state);

        using var writer = new StringWriter();
        formatter.Write(entry, scopeProvider: null, writer);
        return writer.ToString();
    }

    [Fact]
    public void Write_RendersCanonicalExampleLine()
    {
        var line = Render(LogLevel.Information, "marketdata.client", "Making request...", exception: null);

        Assert.Equal(
            "2025-02-21 12:00:00 - marketdata.client - INFO - Making request...\n",
            line);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "DEBUG")]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO")]
    [InlineData(LogLevel.Warning, "WARNING")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "ERROR")]
    public void Write_MapsEveryLogLevelToSpecVocabulary(LogLevel level, string expected)
    {
        var line = Render(level, "marketdata.client", "message", exception: null);

        Assert.Equal($"{ExpectedTimestamp} - marketdata.client - {expected} - message\n", line);
    }

    [Fact]
    public void Write_AppendsExceptionAfterMessage()
    {
        // A non-thrown exception has a null stack trace, so ToString() is deterministic:
        // "{TypeFullName}: {Message}".
        var exception = new InvalidOperationException("boom");

        var line = Render(LogLevel.Error, "marketdata.client", "Request failed", exception);

        Assert.Equal(
            $"{ExpectedTimestamp} - marketdata.client - ERROR - Request failed System.InvalidOperationException: boom\n",
            line);
    }

    [Fact]
    public void Write_WithExceptionAndEmptyMessage_RendersOnlyException()
    {
        var exception = new InvalidOperationException("boom");

        var line = Render(LogLevel.Error, "marketdata.client", message: string.Empty, exception);

        Assert.Equal(
            $"{ExpectedTimestamp} - marketdata.client - ERROR - System.InvalidOperationException: boom\n",
            line);
    }

    [Fact]
    public void Write_WithEmptyMessageAndNoException_WritesNothing()
    {
        var line = Render(LogLevel.Information, "marketdata.client", message: string.Empty, exception: null);

        Assert.Equal(string.Empty, line);
    }

    [Fact]
    public void DefaultConstructor_UsesSystemClockAndSetsFormatterName()
    {
        // Exercises the public parameterless constructor (the one dependency injection uses) without
        // depending on wall-clock time — only the formatter name is asserted.
        var formatter = new MarketDataConsoleFormatter();

        Assert.Equal(MarketDataConsoleFormatter.FormatterName, formatter.Name);
        Assert.Equal("marketdata", MarketDataConsoleFormatter.FormatterName);
    }
}

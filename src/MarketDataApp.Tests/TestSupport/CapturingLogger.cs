using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.TestSupport;

/// <summary>
/// A test <see cref="ILogger"/> that records every entry it receives. <see cref="IsEnabled"/>
/// always returns <see langword="true"/>, so any suppression observed in a test is the SDK's own
/// MARKETDATA_LOGGING_LEVEL gating rather than the logger declining the level.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

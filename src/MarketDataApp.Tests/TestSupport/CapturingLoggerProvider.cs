using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.TestSupport;

/// <summary>
/// A test <see cref="ILoggerProvider"/> that records every entry from every category into a single
/// shared list, so DI-wired SDK logging can be asserted end-to-end. Register it via
/// <c>services.AddLogging(b => b.AddProvider(new CapturingLoggerProvider()))</c>; each entry keeps
/// its category name so tests can assert the SDK logs under <c>MarketDataApp.MarketDataClient</c>.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CategoryLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CategoryLogger(
        string category,
        ConcurrentQueue<(string Category, LogLevel Level, string Message)> entries) : ILogger
    {
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
            entries.Enqueue((category, logLevel, formatter(state, exception)));
        }
    }
}

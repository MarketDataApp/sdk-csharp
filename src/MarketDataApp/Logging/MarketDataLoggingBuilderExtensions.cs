using MarketDataApp.Logging;
using Microsoft.Extensions.Logging.Console;

// Placed in the Microsoft.Extensions.Logging namespace (the framework convention for logging-builder
// extensions) so AddMarketDataCanonicalConsole is discoverable from Program.cs without an extra
// `using`.
namespace Microsoft.Extensions.Logging;

/// <summary>
/// Logging-builder extensions that wire up the SDK's opt-in canonical console formatter.
/// </summary>
public static class MarketDataLoggingBuilderExtensions
{
    /// <summary>
    /// Adds the console logging provider configured with <see cref="MarketDataConsoleFormatter"/> so
    /// that log lines are rendered in the canonical MarketData.app layout
    /// <c>{timestamp} - {logger_name} - {level} - {message}</c> — for example
    /// <c>2025-02-21 12:00:00 - marketdata.client - INFO - Making request...</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>opt-in</b>. By default the SDK emits structured
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/> events and leaves the rendered text layout
    /// to whichever provider you attach. Call this method when you want the built-in console provider
    /// to produce the exact canonical line.
    /// </para>
    /// <para>
    /// For non-console providers (Serilog, NLog, OpenTelemetry, …) configure an equivalent output
    /// template instead; the SDK's structured events and their properties are identical regardless of
    /// the provider.
    /// </para>
    /// </remarks>
    /// <param name="builder">The logging builder to configure.</param>
    /// <returns>The same <see cref="ILoggingBuilder"/> so calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ILoggingBuilder AddMarketDataCanonicalConsole(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddConsole(options => options.FormatterName = MarketDataConsoleFormatter.FormatterName);
        builder.AddConsoleFormatter<MarketDataConsoleFormatter, ConsoleFormatterOptions>();
        return builder;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Logging;

/// <summary>
/// Tests for the <c>AddMarketDataCanonicalConsole</c> logging-builder extension.
/// </summary>
public sealed class MarketDataLoggingBuilderExtensionsTests
{
    [Fact]
    public void AddMarketDataCanonicalConsole_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarketDataLoggingBuilderExtensions.AddMarketDataCanonicalConsole(null!));
    }

    [Fact]
    public void AddMarketDataCanonicalConsole_RegistersConsoleAndReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        ILoggingBuilder? capturedBuilder = null;
        ILoggingBuilder? returnedBuilder = null;

        services.AddLogging(builder =>
        {
            capturedBuilder = builder;
            returnedBuilder = builder.AddMarketDataCanonicalConsole();
        });

        // The extension returns the same builder so registration calls can be chained.
        Assert.Same(capturedBuilder, returnedBuilder);

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ILoggerFactory>();
        Assert.NotNull(factory);

        // Creating a logger constructs the console provider, which reads ConsoleLoggerOptions and
        // therefore runs the FormatterName configuration lambda registered by the extension.
        var logger = factory.CreateLogger("marketdata.client");
        Assert.NotNull(logger);
    }
}

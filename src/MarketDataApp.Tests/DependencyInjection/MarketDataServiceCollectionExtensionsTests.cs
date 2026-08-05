using System.Net.Http;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.DependencyInjection;

public sealed class MarketDataServiceCollectionExtensionsTests
{
    private const string HttpClientName = "MarketDataApp";

    [Fact]
    public void AddMarketDataClient_Environment_RegistersClientAsSingleton()
    {
        var services = new ServiceCollection();

        var returned = services.AddMarketDataClient();

        // The extension returns the same collection so registration calls can be chained.
        Assert.Same(services, returned);

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<MarketDataClient>();
        var second = provider.GetRequiredService<MarketDataClient>();

        // A single MarketDataClient holds shared state and must be a singleton.
        Assert.Same(first, second);
    }

    [Fact]
    public void AddMarketDataClient_Configuration_BindsOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MARKETDATA_TOKEN"] = "config-token",
                ["MARKETDATA_BASE_URL"] = "https://example.test/api/"
            })
            .Build();

        var services = new ServiceCollection();

        var returned = services.AddMarketDataClient(configuration);
        Assert.Same(services, returned);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MarketDataClientOptions>();

        Assert.Equal("config-token", options.ApiToken);
        Assert.Equal(new Uri("https://example.test/api/"), options.BaseAddress);
    }

    [Fact]
    public void AddMarketDataClient_ExplicitOptions_UsesSuppliedInstance()
    {
        var options = new MarketDataClientOptions { ApiToken = "explicit-token" };
        var services = new ServiceCollection();

        var returned = services.AddMarketDataClient(options);
        Assert.Same(services, returned);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<MarketDataClientOptions>();

        Assert.Same(options, resolved);
    }

    [Fact]
    public void AddMarketDataClient_ResolvesClientAndNamedHttpClient()
    {
        var services = new ServiceCollection();
        services.AddMarketDataClient(new MarketDataClientOptions());

        using var provider = services.BuildServiceProvider();

        // Resolving the client exercises the singleton factory lambda.
        var client = provider.GetRequiredService<MarketDataClient>();
        Assert.NotNull(client);

        // The named HttpClient is creatable via the factory; creating it exercises the
        // ConfigurePrimaryHttpMessageHandler lambda (the SDK default handler).
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = factory.CreateClient(HttpClientName);
        Assert.NotNull(httpClient);
    }

    // The category name the framework derives for ILogger<MarketDataClient>: the fully-qualified
    // type name with '.' delimiters. The SDK's initialization log surfaces under this category.
    private const string ClientLogCategory = "MarketDataApp.MarketDataClient";

    [Fact]
    public void AddMarketDataClient_AutoWiresContainerLogger_EmitsInitializationLog()
    {
        // A capturing provider registered through AddLogging makes ILogger<MarketDataClient>
        // resolvable, so the singleton factory auto-wires it into the SDK options.
        var capturing = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(capturing));
        services.AddMarketDataClient(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        // Resolving (constructing) the client emits the Information-level initialization log through
        // the auto-wired container logger; the ApiClient constructor writes it via options.Logger.
        _ = provider.GetRequiredService<MarketDataClient>();

        Assert.Contains(
            capturing.Entries,
            entry => entry.Category == ClientLogCategory
                && entry.Level == LogLevel.Information
                && entry.Message.Contains("Market Data client initialized", StringComparison.Ordinal));
    }

    [Fact]
    public void AddMarketDataClient_ExplicitLogger_IsNotReplacedByContainerLogger()
    {
        // A container logger IS available, but the caller supplied an explicit logger via options.
        var containerProvider = new CapturingLoggerProvider();
        var explicitLogger = new CapturingLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(containerProvider));
        services.AddMarketDataClient(new MarketDataClientOptions { Logger = explicitLogger });

        using var provider = services.BuildServiceProvider();

        // Constructing the client must log through the explicit logger, not the container one.
        _ = provider.GetRequiredService<MarketDataClient>();

        Assert.Contains(
            explicitLogger.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("Market Data client initialized", StringComparison.Ordinal));

        // Auto-wire did not override the explicit logger, so the SDK category never reached the
        // container provider.
        Assert.DoesNotContain(
            containerProvider.Entries,
            entry => entry.Category == ClientLogCategory);
    }

    [Fact]
    public void AddMarketDataClient_NoLoggerResolvable_ConstructsClientWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddMarketDataClient(new ConfigurationBuilder().Build());

        // AddHttpClient (invoked internally) registers the logging infrastructure, so drop the
        // open-generic ILogger<> registration to simulate a container without logging: the factory's
        // ILogger<MarketDataClient> lookup then resolves to null and the logger stays unset.
        var loggerDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(ILogger<>));
        services.Remove(loggerDescriptor);

        using var provider = services.BuildServiceProvider();

        // Resolving the client exercises the factory with a null resolved logger; it must construct
        // silently rather than throw.
        var client = provider.GetRequiredService<MarketDataClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddMarketDataClient_NullOptions_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddMarketDataClient((MarketDataClientOptions)null!));
    }

    [Fact]
    public void AddMarketDataClient_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddMarketDataClient((IConfiguration)null!));
    }
}

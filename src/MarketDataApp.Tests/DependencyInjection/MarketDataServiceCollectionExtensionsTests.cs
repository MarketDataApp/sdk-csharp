using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

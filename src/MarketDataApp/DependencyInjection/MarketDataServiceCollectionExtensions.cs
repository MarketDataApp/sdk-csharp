using MarketDataApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Placed in the Microsoft.Extensions.DependencyInjection namespace (the framework convention for
// service-registration extensions) so AddMarketDataClient is discoverable from Program.cs without an
// extra `using`.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods that register <see cref="MarketDataClient"/> with a
/// <see cref="IServiceCollection"/> for ASP.NET Core and generic-host applications.
/// </summary>
/// <remarks>
/// <para>
/// Each overload registers <see cref="MarketDataClient"/> as a <b>singleton</b> — a single instance
/// is documented safe for concurrent use and holds shared client-wide state (the concurrency
/// semaphore, the rate-limit snapshot, and the <c>/status</c> cache). The client is built over an
/// <see cref="IHttpClientFactory"/>-managed <see cref="System.Net.Http.HttpClient"/> whose primary
/// handler is <see cref="MarketDataClient.CreateDefaultHttpHandler"/>, so the 2-second connection
/// timeout applies and pooled connections rotate DNS; the HttpClient-level timeout is disabled in
/// favor of the SDK's fixed 99-second request timeout. These are defaults, not enforcement: an
/// application that configures the same named client after calling the extension overrides them.
/// </para>
/// <para>
/// The dependency-injection path uses the <see cref="MarketDataClient"/> constructor, so when a
/// token is configured and <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> is
/// <c>true</c> (the default), the token is validated with a blocking <c>GET /user/</c> the first
/// time the singleton is resolved — typically during startup wiring — and an invalid token throws
/// <see cref="MarketDataApp.Exceptions.AuthenticationException"/> at that point. Register options
/// with <c>ValidateTokenOnStartup = false</c> to defer errors to the first request instead.
/// </para>
/// <para>
/// Registrations use <c>TryAdd*</c> so an application can override either
/// <see cref="MarketDataClientOptions"/> or <see cref="MarketDataClient"/> by registering its own
/// before calling these methods.
/// </para>
/// </remarks>
public static class MarketDataServiceCollectionExtensions
{
    private const string HttpClientName = "MarketDataApp";

    /// <summary>
    /// Registers <see cref="MarketDataClient"/> as a singleton, resolving
    /// <see cref="MarketDataClientOptions"/> from <see cref="MarketDataClientOptions.FromEnvironment"/>
    /// (user secrets, an optional <c>.env</c> file, and process environment variables).
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMarketDataClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return AddMarketDataClientCore(services, MarketDataClientOptions.FromEnvironment());
    }

    /// <summary>
    /// Registers <see cref="MarketDataClient"/> as a singleton using the supplied
    /// <see cref="MarketDataClientOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="options">The fully built client options to use.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddMarketDataClient(
        this IServiceCollection services,
        MarketDataClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        return AddMarketDataClientCore(services, options);
    }

    /// <summary>
    /// Registers <see cref="MarketDataClient"/> as a singleton, resolving
    /// <see cref="MarketDataClientOptions"/> from the supplied <see cref="IConfiguration"/> via
    /// <see cref="MarketDataClientOptions.FromConfiguration"/> (reads the <c>MARKETDATA_*</c> keys).
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configuration">Configuration containing the <c>MARKETDATA_*</c> keys.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddMarketDataClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return AddMarketDataClientCore(services, MarketDataClientOptions.FromConfiguration(configuration));
    }

    private static IServiceCollection AddMarketDataClientCore(
        IServiceCollection services,
        MarketDataClientOptions options)
    {
        // Back the named client with the SDK's default handler so the 2-second connect timeout
        // applies and pooled connections rotate DNS / honor load-balancer changes. The
        // HttpClient-level timeout is disabled because the SDK enforces its own fixed 99-second
        // per-attempt timeout; both are defaults only — an application that configures the same
        // named client after calling AddMarketDataClient overrides them (its actions run later).
        services.AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => MarketDataClient.CreateDefaultHttpHandler())
            .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan);

        // TryAdd* lets an application override the options or the client by registering its own first.
        services.TryAddSingleton(options);
        services.TryAddSingleton<MarketDataClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opts = sp.GetRequiredService<MarketDataClientOptions>();

            // Auto-wire the SDK logger from the container so AddMarketDataClient() emits SDK logs
            // whenever the app has logging configured (pairs with AddMarketDataCanonicalConsole()).
            // Only fill in the logger when the caller did not supply one explicitly, and only when
            // the container actually has logging registered; if no logger is resolvable the logger
            // stays unset and the client still constructs silently.
            if (opts.Logger is null)
            {
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<MarketDataClient>>();
                if (logger is not null)
                {
                    opts = opts with { Logger = logger };
                }
            }

            return new MarketDataClient(factory.CreateClient(HttpClientName), opts);
        });

        return services;
    }
}

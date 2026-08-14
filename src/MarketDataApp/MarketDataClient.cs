using System.Net.Http;

namespace MarketDataApp;

/// <summary>
/// Entry point for asynchronous access to the Market Data API.
/// </summary>
public sealed class MarketDataClient : IDisposable
{
    private readonly ApiClient _apiClient;
    // Set only when the SDK created the HttpClient itself (the overloads without an HttpClient
    // parameter); a caller-supplied client is never stored here and never disposed by the SDK.
    private readonly HttpClient? _ownedHttpClient;

    /// <summary>
    /// Creates a <see cref="SocketsHttpHandler"/> pre-configured with the mandated 2-second
    /// connection timeout (§10) and sensible connection-pool defaults. The SDK-owned client
    /// created by the overloads without an <see cref="HttpClient"/> parameter is backed by this
    /// handler automatically; callers who manage their own client can use it the same way:
    /// <c>new HttpClient(MarketDataClient.CreateDefaultHttpHandler())</c>.
    /// </summary>
    /// <remarks>
    /// The SDK always enforces its own fixed 99-second per-request timeout internally. It cannot,
    /// however, apply a separate connection timeout, because the connect timeout is a property of
    /// the <see cref="HttpClient"/>'s handler, which the application owns. Use this factory to obtain
    /// a handler that supplies the 2-second connect (TCP + TLS handshake) timeout. Callers that
    /// already configure their own handler can set <see cref="SocketsHttpHandler.ConnectTimeout"/>
    /// to <c>TimeSpan.FromSeconds(2)</c> directly instead.
    /// </remarks>
    /// <returns>A new handler; the caller owns and disposes it (usually via the <see cref="HttpClient"/>).</returns>
    public static SocketsHttpHandler CreateDefaultHttpHandler() =>
        new()
        {
            // §10: the separate 2-second connection timeout. The 99-second request timeout is
            // enforced by the SDK itself and is intentionally not configurable here.
            ConnectTimeout = TimeSpan.FromSeconds(2),
            // Recycle pooled connections periodically so long-lived clients honor DNS changes and
            // load-balancer rotation without leaking or reusing stale sockets indefinitely.
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        };

    /// <summary>
    /// Creates a client using the supplied <see cref="HttpClient"/>. When an API token is present
    /// and <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> is <c>true</c> (the
    /// default), the constructor performs a blocking <c>GET /user/</c> to fail fast on an invalid
    /// token (throwing <see cref="Exceptions.AuthenticationException"/>) and to seed
    /// <see cref="LatestRateLimit"/>. In demo mode (no token), or when validation is disabled,
    /// the constructor performs no network I/O and authentication errors surface on the first
    /// request. The supplied client is never reconfigured: its handler and
    /// <see cref="HttpClient.Timeout"/> are respected as-is. Prefer
    /// <see cref="CreateAsync(HttpClient, MarketDataClientOptions?, CancellationToken)"/> where
    /// the call site can await: it runs the same validation without blocking the calling thread.
    /// </summary>
    /// <param name="httpClient">HTTP client managed by the application or dependency-injection container.</param>
    /// <param name="options">Optional client configuration.</param>
    public MarketDataClient(HttpClient httpClient, MarketDataClientOptions? options = null)
        : this(
            httpClient,
            options ?? MarketDataClientOptions.FromEnvironment(),
            validateSynchronously: true,
            ownsHttpClient: false)
    {
    }

    /// <summary>
    /// Creates a client over an SDK-owned <see cref="HttpClient"/> configured with the SDK
    /// requirements: the <see cref="CreateDefaultHttpHandler"/> transport (2-second connection
    /// timeout, pooled-connection rotation) and no HttpClient-level timeout, because the SDK
    /// enforces its fixed 99-second request timeout internally. Use this when the application is
    /// not explicitly managing an <see cref="HttpClient"/>; <see cref="Dispose"/> also disposes
    /// the owned client. Startup token validation behaves exactly like the
    /// <see cref="MarketDataClient(HttpClient, MarketDataClientOptions?)"/> constructor.
    /// </summary>
    /// <param name="options">Optional client configuration.</param>
    public MarketDataClient(MarketDataClientOptions? options = null)
        : this(
            CreateOwnedHttpClient(),
            options ?? MarketDataClientOptions.FromEnvironment(),
            validateSynchronously: true,
            ownsHttpClient: true)
    {
    }

    private MarketDataClient(
        HttpClient httpClient,
        MarketDataClientOptions options,
        bool validateSynchronously,
        bool ownsHttpClient)
    {
        _ownedHttpClient = ownsHttpClient ? httpClient : null;
        try
        {
            _apiClient = new ApiClient(httpClient, options);
            Utilities = new UtilitiesApi(_apiClient);
            Markets = new MarketsApi(_apiClient);
            Stocks = new StocksApi(_apiClient);
            Funds = new FundsApi(_apiClient);
            Options = new OptionsApi(_apiClient);
            if (validateSynchronously && _apiClient.ShouldValidateTokenOnStartup)
            {
                try
                {
                    _apiClient.ValidateTokenAndSeedRateLimit();
                }
                catch
                {
                    // The instance never escapes a throwing constructor: release the ApiClient-owned
                    // concurrency gate before surfacing the validation failure.
                    _apiClient.Dispose();
                    throw;
                }
            }
        }
        catch when (_ownedHttpClient is not null)
        {
            // Nothing else can release the SDK-created HttpClient once the constructor throws;
            // a caller-supplied client is left untouched.
            _ownedHttpClient.Dispose();
            throw;
        }
    }

    // The SDK-owned transport: the default handler supplies the 2-second connect timeout (§10)
    // and the HttpClient-level timeout is disabled so it cannot race the SDK's own fixed
    // 99-second per-attempt timeout.
    private static HttpClient CreateOwnedHttpClient() =>
        new(CreateDefaultHttpHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

    /// <summary>
    /// Creates a client and, when an API token is present and
    /// <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> is <c>true</c> (the default),
    /// performs an asynchronous <c>GET /user/</c> to fail fast on an invalid token (throwing
    /// <see cref="Exceptions.AuthenticationException"/>) and to seed <see cref="LatestRateLimit"/>.
    /// In demo mode (no token) or when validation is disabled, no request is made and the client
    /// is returned immediately. This is the idiomatic async alternative to the constructor's
    /// blocking startup validation.
    /// </summary>
    /// <param name="httpClient">HTTP client managed by the application or dependency-injection container.</param>
    /// <param name="options">Optional client configuration.</param>
    /// <param name="cancellationToken">Token that cancels the startup validation request.</param>
    public static Task<MarketDataClient> CreateAsync(
        HttpClient httpClient,
        MarketDataClientOptions? options = null,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(
            httpClient,
            options ?? MarketDataClientOptions.FromEnvironment(),
            ownsHttpClient: false,
            cancellationToken);

    /// <summary>
    /// Creates a client over an SDK-owned <see cref="HttpClient"/> configured with the SDK
    /// requirements (see <see cref="MarketDataClient(MarketDataClientOptions?)"/>) and, when an
    /// API token is present and <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> is
    /// <c>true</c> (the default), validates the token asynchronously exactly like
    /// <see cref="CreateAsync(HttpClient, MarketDataClientOptions?, CancellationToken)"/>.
    /// <see cref="Dispose"/> also disposes the owned client.
    /// </summary>
    /// <param name="options">Optional client configuration.</param>
    /// <param name="cancellationToken">Token that cancels the startup validation request.</param>
    public static Task<MarketDataClient> CreateAsync(
        MarketDataClientOptions? options = null,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(
            CreateOwnedHttpClient(),
            options ?? MarketDataClientOptions.FromEnvironment(),
            ownsHttpClient: true,
            cancellationToken);

    private static async Task<MarketDataClient> CreateAsyncCore(
        HttpClient httpClient,
        MarketDataClientOptions options,
        bool ownsHttpClient,
        CancellationToken cancellationToken)
    {
        var client = new MarketDataClient(httpClient, options, validateSynchronously: false, ownsHttpClient);
        if (client._apiClient.ShouldValidateTokenOnStartup)
        {
            try
            {
                await client._apiClient.ValidateTokenAndSeedRateLimitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        return client;
    }

    /// <summary>Utility endpoints.</summary>
    public UtilitiesApi Utilities { get; }
    /// <summary>Market-calendar endpoints.</summary>
    public MarketsApi Markets { get; }
    /// <summary>Stock endpoints.</summary>
    public StocksApi Stocks { get; }
    /// <summary>Fund and ETF endpoints.</summary>
    public FundsApi Funds { get; }
    /// <summary>Options endpoints.</summary>
    public OptionsApi Options { get; }
    /// <summary>Latest complete rate-limit snapshot received by this client.</summary>
    public RateLimitSnapshot? LatestRateLimit => _apiClient.LatestRateLimit;

    /// <summary>
    /// Releases resources owned by this client, including the <see cref="HttpClient"/> the SDK
    /// created for the overloads without an <see cref="HttpClient"/> parameter. A caller-supplied
    /// <see cref="HttpClient"/> is intentionally not disposed.
    /// </summary>
    public void Dispose()
    {
        _apiClient.Dispose();
        _ownedHttpClient?.Dispose();
    }
}

namespace MarketDataApp;

/// <summary>
/// Entry point for asynchronous access to the Market Data API.
/// </summary>
public sealed class MarketDataClient : IDisposable
{
    private readonly ApiClient _apiClient;

    /// <summary>
    /// Creates a client using the supplied <see cref="HttpClient"/>. This constructor performs
    /// no network I/O and no startup token validation; authentication and rate-limit errors
    /// surface on the first request. Use <see cref="CreateAsync"/> to validate the token and
    /// seed the rate-limit snapshot at startup.
    /// </summary>
    /// <param name="httpClient">HTTP client managed by the application or dependency-injection container.</param>
    /// <param name="options">Optional client configuration.</param>
    public MarketDataClient(HttpClient httpClient, MarketDataClientOptions? options = null)
    {
        options ??= MarketDataClientOptions.FromEnvironment();
        _apiClient = new ApiClient(httpClient, options);
        Utilities = new UtilitiesApi(_apiClient);
        Markets = new MarketsApi(_apiClient);
        Stocks = new StocksApi(_apiClient);
        Funds = new FundsApi(_apiClient);
        Options = new OptionsApi(_apiClient);
    }

    /// <summary>
    /// Creates a client and, when an API token is present and
    /// <see cref="MarketDataClientOptions.ValidateTokenOnStartup"/> is <c>true</c> (the default),
    /// performs an asynchronous <c>GET /user/</c> to fail fast on an invalid token (throwing
    /// <see cref="Exceptions.AuthenticationException"/>) and to seed <see cref="LatestRateLimit"/>.
    /// In demo mode (no token) or when validation is disabled, no request is made and the client
    /// is returned immediately. This is the idiomatic async alternative to blocking startup
    /// validation in a constructor.
    /// </summary>
    /// <param name="httpClient">HTTP client managed by the application or dependency-injection container.</param>
    /// <param name="options">Optional client configuration.</param>
    /// <param name="cancellationToken">Token that cancels the startup validation request.</param>
    public static async Task<MarketDataClient> CreateAsync(
        HttpClient httpClient,
        MarketDataClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= MarketDataClientOptions.FromEnvironment();
        var client = new MarketDataClient(httpClient, options);
        if (options.ValidateTokenOnStartup && !string.IsNullOrWhiteSpace(options.ApiToken))
        {
            await client._apiClient.ValidateTokenAndSeedRateLimitAsync(cancellationToken)
                .ConfigureAwait(false);
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
    /// Releases resources owned by this client. The caller-owned
    /// <see cref="HttpClient"/> supplied to the constructor is intentionally not disposed.
    /// </summary>
    public void Dispose() => _apiClient.Dispose();
}

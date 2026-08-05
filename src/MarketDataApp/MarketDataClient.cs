namespace MarketDataApp;

/// <summary>
/// Entry point for asynchronous access to the Market Data API.
/// </summary>
public sealed class MarketDataClient : IDisposable
{
    private readonly ApiClient _apiClient;

    /// <summary>Creates a client using the supplied <see cref="HttpClient"/>.</summary>
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

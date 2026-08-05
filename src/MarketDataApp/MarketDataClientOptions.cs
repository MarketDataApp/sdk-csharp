using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace MarketDataApp;

/// <summary>Configuration for <see cref="MarketDataClient"/>.</summary>
public sealed record MarketDataClientOptions
{
    private static readonly string DefaultUserAgentValue = CreateDefaultUserAgent();

    /// <summary>Bearer token used for authenticated requests.</summary>
    public string? ApiToken { get; init; }
    /// <summary>API host URI.</summary>
    public Uri BaseAddress { get; init; } = new("https://api.marketdata.app/");
    /// <summary>Version path segment used by versioned endpoints.</summary>
    public string ApiVersion { get; init; } = "v1";
    /// <summary>Maximum number of retries after a transient request failure.</summary>
    public int MaxRetries { get; init; } = 3;
    /// <summary>Initial exponential-backoff delay between retries.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>Maximum exponential-backoff delay when the server does not provide Retry-After.</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Maximum server-provided Retry-After delay honored by automatic retries.</summary>
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromMinutes(10);
    /// <summary>Fractional random jitter applied to exponential backoff, from 0 through 1.</summary>
    public double RetryJitterFactor { get; init; }
    /// <summary>Maximum number of HTTP requests simultaneously dispatched by this client.</summary>
    public int MaxConcurrentRequests { get; init; } = 50;
    /// <summary>Time source used by timeout, retry, and rate-limit behavior.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    /// <summary>User-agent value sent by the client.</summary>
    public string UserAgent { get; init; } = DefaultUserAgentValue;
    /// <summary>Logger used for SDK lifecycle, request, response, and error diagnostics.</summary>
    public ILogger? Logger { get; init; }
    /// <summary>
    /// Validates an authenticated token with <c>/user/</c> during client construction.
    /// Set to <c>false</c> for short-lived runtimes that prefer first-request (lazy) validation.
    /// </summary>
    public bool ValidateTokenOnStartup { get; init; } = true;

    /// <summary>
    /// Loads MARKETDATA_* values from user secrets, an optional .env file, and process
    /// environment variables, in increasing precedence order.
    /// </summary>
    public static MarketDataClientOptions FromEnvironment()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<MarketDataClientOptions>(optional: true);

        if (File.Exists(".env"))
        {
            builder.AddDotNetEnv(".env", new DotNetEnv.LoadOptions(clobberExistingVars: false));
        }

        var configuration = builder
            .AddEnvironmentVariables()
            .Build();
        return FromConfiguration(configuration);
    }

    /// <summary>
    /// Creates client options from configuration containing MARKETDATA_* keys.
    /// Legacy sectioned keys are intentionally not supported.
    /// </summary>
    public static MarketDataClientOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new MarketDataClientOptions
        {
            ApiToken = configuration["MARKETDATA_TOKEN"],
            BaseAddress = ReadUri(configuration["MARKETDATA_BASE_URL"]),
            ApiVersion = configuration["MARKETDATA_API_VERSION"] ?? "v1",
            MaxRetries = ReadInt(configuration, "MARKETDATA_MAX_RETRIES", 3),
            RetryBaseDelay = ReadTimeSpan(configuration, "MARKETDATA_RETRY_BASE_DELAY", TimeSpan.FromSeconds(1)),
            RetryMaxDelay = ReadTimeSpan(configuration, "MARKETDATA_RETRY_MAX_DELAY", TimeSpan.FromSeconds(30)),
            MaxRetryAfter = ReadTimeSpan(configuration, "MARKETDATA_MAX_RETRY_AFTER", TimeSpan.FromMinutes(10)),
            RetryJitterFactor = ReadDouble(configuration, "MARKETDATA_RETRY_JITTER_FACTOR", 0),
            MaxConcurrentRequests = ReadInt(configuration, "MARKETDATA_MAX_CONCURRENT_REQUESTS", 50),
            UserAgent = configuration["MARKETDATA_USER_AGENT"] ?? DefaultUserAgentValue
        };
    }

    private static Uri ReadUri(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new Uri("https://api.marketdata.app/")
            : new Uri(value, UriKind.Absolute);

    private static int ReadInt(IConfiguration configuration, string name, int defaultValue)
    {
        var configured = configuration[name];
        if (configured is null)
        {
            return defaultValue;
        }

        return int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be an integer.");
    }

    private static double ReadDouble(IConfiguration configuration, string name, double defaultValue)
    {
        var configured = configuration[name];
        if (configured is null)
        {
            return defaultValue;
        }

        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be a number.");
    }

    private static TimeSpan ReadTimeSpan(
        IConfiguration configuration,
        string name,
        TimeSpan defaultValue) =>
        ReadTimeSpanValue(configuration[name], name, defaultValue);

    private static TimeSpan ReadTimeSpanValue(string? configured, string name, TimeSpan defaultValue)
    {
        if (configured is null)
        {
            return defaultValue;
        }

        return TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be a TimeSpan.");
    }

    private static string CreateDefaultUserAgent()
    {
        var assembly = typeof(MarketDataClientOptions).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "unknown";
        return $"marketdata-sdk-csharp/{version}";
    }
}

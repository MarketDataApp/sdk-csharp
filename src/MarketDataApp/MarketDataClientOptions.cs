using DotNetEnv.Configuration;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Reflection;
using System.Text;
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
    /// <summary>
    /// Test seam for the retry-backoff jitter sampler; returns a value in <c>[0, 1)</c>. When
    /// <see langword="null"/> (the default, and the only production value) the client uses
    /// <see cref="Random.Shared"/>. Tests inject a deterministic sampler so the jittered-backoff and
    /// max-delay clamp branches are exercised deterministically. Not read from configuration.
    /// </summary>
    internal Func<double>? RetryJitterSource { get; init; }
    /// <summary>Maximum number of HTTP requests simultaneously dispatched by this client.</summary>
    public int MaxConcurrentRequests { get; init; } = 50;
    /// <summary>Time source used by timeout, retry, and rate-limit behavior.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    /// <summary>User-agent value sent by the client.</summary>
    public string UserAgent { get; init; } = DefaultUserAgentValue;
    /// <summary>Logger used for SDK lifecycle, request, response, and error diagnostics.</summary>
    public ILogger? Logger { get; init; }
    /// <summary>
    /// Client-level default response date/time format, used when a per-request
    /// <see cref="MarketDataRequestOptions.DateFormat"/> is not supplied (§4 cascade). Read from
    /// <c>MARKETDATA_DATE_FORMAT</c> (<c>unix</c>/<c>timestamp</c>/<c>spreadsheet</c>).
    /// </summary>
    public DateFormat? DefaultDateFormat { get; init; }
    /// <summary>
    /// Client-level default data mode, used when a per-request
    /// <see cref="MarketDataRequestOptions.Mode"/> is not supplied (§4 cascade). Read from
    /// <c>MARKETDATA_MODE</c> (<c>live</c>/<c>cached</c>/<c>delayed</c>).
    /// </summary>
    public Mode? DefaultMode { get; init; }
    /// <summary>
    /// Client-level default response columns, used when a per-request
    /// <see cref="MarketDataRequestOptions.Columns"/> is not supplied (§4 cascade). Read from the
    /// comma-separated <c>MARKETDATA_COLUMNS</c>.
    /// </summary>
    public IReadOnlyList<string>? DefaultColumns { get; init; }
    /// <summary>
    /// Client-level default for whether CSV output includes a header row, used when a per-request
    /// <see cref="MarketDataRequestOptions.Headers"/> is not supplied (§4 cascade). Read from
    /// <c>MARKETDATA_ADD_HEADERS</c>. Applies to CSV requests only.
    /// </summary>
    public bool? DefaultAddHeaders { get; init; }
    /// <summary>
    /// Client-level default for human-readable CSV output, used when a per-request
    /// <see cref="MarketDataRequestOptions.Human"/> is not supplied (§4 cascade). Read from
    /// <c>MARKETDATA_USE_HUMAN_READABLE</c>. Applies to CSV / human-readable output only.
    /// </summary>
    public bool? DefaultHuman { get; init; }
    /// <summary>
    /// Minimum severity a SDK diagnostic must reach to be emitted through <see cref="Logger"/>
    /// (default <see cref="LogLevel.Information"/>, so the Debug request/response logs are
    /// suppressed unless DEBUG is configured). Read from <c>MARKETDATA_LOGGING_LEVEL</c>
    /// (<c>DEBUG</c>/<c>INFO</c>/<c>WARNING</c>/<c>ERROR</c>, or a .NET <see cref="LogLevel"/> name).
    /// </summary>
    public LogLevel? MinimumLogLevel { get; init; } = LogLevel.Information;
    /// <summary>
    /// Advisory preferred output format read from <c>MARKETDATA_OUTPUT_FORMAT</c>
    /// (<c>json</c>/<c>csv</c>). In this SDK the effective format is determined by which method is
    /// called — typed methods return JSON-decoded models and the <c>*CsvAsync</c> methods return
    /// CSV — so this value is default-hinting only and never reroutes a typed method to CSV.
    /// </summary>
    public OutputFormat? OutputFormat { get; init; }
    /// <summary>
    /// Gates whether <see cref="MarketDataClient.CreateAsync"/> validates an authenticated token
    /// with <c>/user/</c> and seeds the rate-limit snapshot at startup (default <c>true</c>).
    /// Set to <c>false</c> for short-lived runtimes that prefer first-request (lazy) validation.
    /// This option has no effect on the <see cref="MarketDataClient"/> constructor, which never
    /// performs startup validation; errors surface on the first request in that path.
    /// </summary>
    public bool ValidateTokenOnStartup { get; init; } = true;

    // Replaces the compiler-generated PrintMembers, which would print ApiToken verbatim and leak
    // the credential into any log that stringifies the options. Tokens may surface with at most
    // their last 4 characters; all other public members keep the generated record format.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ApiToken = ")
            .Append(string.IsNullOrEmpty(ApiToken) ? ApiToken : ApiClient.RedactToken(ApiToken));
        builder.Append(", BaseAddress = ").Append(BaseAddress);
        builder.Append(", ApiVersion = ").Append(ApiVersion);
        builder.Append(", MaxRetries = ").Append(MaxRetries);
        builder.Append(", RetryBaseDelay = ").Append(RetryBaseDelay);
        builder.Append(", RetryMaxDelay = ").Append(RetryMaxDelay);
        builder.Append(", MaxRetryAfter = ").Append(MaxRetryAfter);
        builder.Append(", RetryJitterFactor = ").Append(RetryJitterFactor);
        builder.Append(", MaxConcurrentRequests = ").Append(MaxConcurrentRequests);
        builder.Append(", TimeProvider = ").Append(TimeProvider);
        builder.Append(", UserAgent = ").Append(UserAgent);
        builder.Append(", Logger = ").Append(Logger);
        builder.Append(", DefaultDateFormat = ").Append(DefaultDateFormat);
        builder.Append(", DefaultMode = ").Append(DefaultMode);
        builder.Append(", DefaultColumns = ").Append(DefaultColumns);
        builder.Append(", DefaultAddHeaders = ").Append(DefaultAddHeaders);
        builder.Append(", DefaultHuman = ").Append(DefaultHuman);
        builder.Append(", MinimumLogLevel = ").Append(MinimumLogLevel);
        builder.Append(", OutputFormat = ").Append(OutputFormat);
        builder.Append(", ValidateTokenOnStartup = ").Append(ValidateTokenOnStartup);
        return true;
    }

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
            UserAgent = configuration["MARKETDATA_USER_AGENT"] ?? DefaultUserAgentValue,
            DefaultDateFormat = ReadDateFormat(configuration, "MARKETDATA_DATE_FORMAT"),
            DefaultMode = ReadMode(configuration, "MARKETDATA_MODE"),
            DefaultColumns = ReadColumns(configuration["MARKETDATA_COLUMNS"]),
            DefaultAddHeaders = ReadBool(configuration, "MARKETDATA_ADD_HEADERS"),
            DefaultHuman = ReadBool(configuration, "MARKETDATA_USE_HUMAN_READABLE"),
            MinimumLogLevel = ReadLogLevel(configuration, "MARKETDATA_LOGGING_LEVEL", LogLevel.Information),
            OutputFormat = ReadOutputFormat(configuration, "MARKETDATA_OUTPUT_FORMAT")
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

    private static DateFormat? ReadDateFormat(IConfiguration configuration, string name)
    {
        var configured = configuration[name];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        return DateFormatExtensions.TryParseWireValue(configured, out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be one of: unix, timestamp, spreadsheet.");
    }

    private static Mode? ReadMode(IConfiguration configuration, string name)
    {
        var configured = configuration[name];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        return ModeExtensions.TryParseWireValue(configured, out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be one of: live, cached, delayed.");
    }

    private static IReadOnlyList<string>? ReadColumns(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var columns = value.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return columns.Length == 0 ? null : columns;
    }

    private static bool? ReadBool(IConfiguration configuration, string name)
    {
        var configured = configuration[name];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        return bool.TryParse(configured.Trim(), out var value)
            ? value
            : throw new FormatException(
                $"Configuration value '{name}' must be a boolean (true or false).");
    }

    private static LogLevel ReadLogLevel(IConfiguration configuration, string name, LogLevel defaultValue)
    {
        var configured = configuration[name];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return defaultValue;
        }

        // Accept the requirement's DEBUG/INFO/WARNING/ERROR names (case-insensitive) plus the
        // full set of .NET LogLevel names so either spelling of, e.g., Information works.
        return configured.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            "none" => LogLevel.None,
            _ => throw new FormatException(
                $"Configuration value '{name}' must be one of: DEBUG, INFO, WARNING, ERROR "
                + "(or a .NET LogLevel name).")
        };
    }

    private static OutputFormat? ReadOutputFormat(IConfiguration configuration, string name)
    {
        var configured = configuration[name];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        // Fully qualified: the OutputFormat property shadows the enum type name in this scope.
        return configured.Trim().ToLowerInvariant() switch
        {
            "json" => MarketDataApp.OutputFormat.Json,
            "csv" => MarketDataApp.OutputFormat.Csv,
            _ => throw new FormatException(
                $"Configuration value '{name}' must be one of: json, csv.")
        };
    }

    // Excluded: the version-fallback branches (missing AssemblyInformationalVersion, then missing
    // AssemblyName.Version, then the literal "unknown") are unreachable in a normally-built
    // assembly. MinVer always stamps AssemblyInformationalVersion, so the primary path is always
    // taken and the "?? ..." fallbacks cannot be exercised.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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

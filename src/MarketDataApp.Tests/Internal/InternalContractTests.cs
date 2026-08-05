using MarketDataApp;
using MarketDataApp.Utilities;
using MarketDataApp.Tests.TestSupport;

namespace MarketDataApp.Tests.Internal;

/// <summary>
/// Tests for internal transport/parsing helper contracts that are unreachable through the public
/// API — dependency-argument guards, null-options tolerance in the query builder, the rootless
/// service-key path in the status gate, and the culture-invariant formatting helpers.
/// </summary>
public sealed class InternalContractTests
{
    private static MarketDataClientOptions ValidOptions() =>
        new() { ValidateTokenOnStartup = false };

    [Fact]
    public void ApiClient_Constructor_RejectsNullDependencies()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

        var nullHttp = Assert.Throws<ArgumentNullException>(() => new ApiClient(null!, ValidOptions()));
        Assert.Equal("httpClient", nullHttp.ParamName);

        var nullOptions = Assert.Throws<ArgumentNullException>(() => new ApiClient(httpClient, null!));
        Assert.Equal("options", nullOptions.ParamName);
    }

    [Fact]
    public void RequestQuery_From_WithNullOptions_ReturnsEmptyQuery()
    {
        var query = RequestQuery.From(null);

        Assert.Empty(query);
    }

    [Fact]
    public void RequestQuery_Csv_WithNullOptions_EmitsOnlyCsvFormat()
    {
        var query = RequestQuery.Csv(null);

        Assert.Equal(new KeyValuePair<string, string?>("format", "csv"), Assert.Single(query));
    }

    [Fact]
    public void RequestValidator_ValidateRequestOptions_ToleratesNull()
    {
        // The null-options guard is a no-op; the assertion is that it does not throw.
        RequestValidator.ValidateRequestOptions(null);
    }

    [Fact]
    public void StatusGate_EvaluateForRetry_ReturnsUnknownForServicelessPath()
    {
        var time = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new StatusGate(
            time,
            "v1",
            _ => Task.FromResult<IReadOnlyList<ServiceStatus>>(Array.Empty<ServiceStatus>()),
            logger: null);
        gate.Record(
        [
            new ServiceStatus("/v1/stocks/quotes/", "online", true, 0.99, 0.99, DateTimeOffset.UnixEpoch)
        ]);

        // A request URI whose path has no service segment ("/") maps to a null service key.
        var availability = gate.EvaluateForRetry(new Uri("https://api.marketdata.app/"));

        Assert.Equal(ServiceAvailability.Unknown, availability);
    }

    [Fact]
    public void ResponseText_FormattingHelpers_HandleNullAndValueCases()
    {
        Assert.Equal("1.5", ResponseText.F((decimal?)1.5m));
        Assert.Equal("n/a", ResponseText.F((decimal?)null));

        Assert.Equal("2.25", ResponseText.F((double?)2.25));
        Assert.Equal("n/a", ResponseText.F((double?)null));

        Assert.Equal("42", ResponseText.F((long?)42L));
        Assert.Equal("n/a", ResponseText.F((long?)null));

        Assert.Equal("7", ResponseText.F((int?)7));
        Assert.Equal("n/a", ResponseText.F((int?)null));

        Assert.Equal("value", ResponseText.F("value"));
        Assert.Equal("n/a", ResponseText.F((string?)null));
        Assert.Equal("n/a", ResponseText.F(string.Empty));

        var timestamp = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Assert.Equal("2024-01-02", ResponseText.D(timestamp));
        Assert.Equal("2024-01-02", ResponseText.D((DateTimeOffset?)timestamp));
        Assert.Equal("n/a", ResponseText.D((DateTimeOffset?)null));
    }

    [Fact]
    public void ResponseText_Truncate_HandlesEmptyShortAndLong()
    {
        Assert.Equal("n/a", ResponseText.Truncate(null));
        Assert.Equal("n/a", ResponseText.Truncate(string.Empty));
        Assert.Equal("short", ResponseText.Truncate("short"));

        var longText = new string('x', 80);
        var truncated = ResponseText.Truncate(longText);
        Assert.EndsWith("…", truncated);
        Assert.Equal(50, truncated.Length);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

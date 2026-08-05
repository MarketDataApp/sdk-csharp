using System.Net;
using System.Text;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;
using MarketDataApp.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace MarketDataApp.Tests.Logging;

/// <summary>
/// Verifies that <see cref="MarketDataClientOptions.MinimumLogLevel"/> (MARKETDATA_LOGGING_LEVEL)
/// gates the SDK's own diagnostics: the Debug request log is suppressed at the default Information
/// threshold and emitted once DEBUG is configured. The <see cref="CapturingLogger"/> always reports
/// <c>IsEnabled == true</c>, so the suppression is the SDK gate rather than the logger.
/// </summary>
public sealed class LoggingLevelTests
{
    [Fact]
    public async Task MinimumLogLevel_Information_SuppressesDebugRequestLog()
    {
        var logger = new CapturingLogger();
        var client = MarketDataTestClient.Create(JsonHandler(), new MarketDataClientOptions
        {
            Logger = logger
            // MinimumLogLevel defaults to Information.
        });

        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Debug);
        // The Information lifecycle log still flows, proving gating is level-based, not off.
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task MinimumLogLevel_Debug_EmitsDebugRequestLog()
    {
        var logger = new CapturingLogger();
        var client = MarketDataTestClient.Create(JsonHandler(), new MarketDataClientOptions
        {
            Logger = logger,
            MinimumLogLevel = LogLevel.Debug
        });

        await client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL"));

        // The Debug request log carries the FULL request URL including the query string
        // (query params are diagnostic, not secret; the token is sent only as an Authorization
        // header). The stocks/prices request appends ?symbols=AAPL, which must appear in the log.
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Debug
                && entry.Message.Contains("Sending GET request")
                && entry.Message.Contains("symbols=AAPL"));
    }

    [Fact]
    public async Task ErrorDiagnostics_EmittedAtTheDefaultInformationThreshold()
    {
        var logger = new CapturingLogger();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom")
            });
        var client = MarketDataTestClient.Create(handler, new MarketDataClientOptions
        {
            Logger = logger,
            MaxRetries = 0
            // MinimumLogLevel defaults to Information; Error >= Information, so it is emitted.
        });

        await Assert.ThrowsAsync<ServerException>(
            () => client.Stocks.GetPricesAsync(new StockPricesRequest("AAPL")));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private static StubHttpMessageHandler JsonHandler() =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"s":"ok","symbol":["AAPL"],"mid":[1.0]}""",
                Encoding.UTF8,
                "application/json")
        });
}

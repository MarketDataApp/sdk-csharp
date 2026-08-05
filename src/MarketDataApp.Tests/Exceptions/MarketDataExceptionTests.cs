using MarketDataApp.Exceptions;

namespace MarketDataApp.Tests.Exceptions;

public sealed class MarketDataExceptionTests
{
    private static readonly string[] SupportLabels =
    [
        "request_id:", "request_url:", "status_code:", "timestamp:", "message:", "exception_type:",
    ];

    [Fact]
    public void SupportInfo_RendersExactBlock_InSpecifiedOrderAndAlignment()
    {
        // Winter timestamp: 12:00 UTC in January is EST (UTC-5) => 07:00 US/Eastern.
        var context = ErrorContext.ForResponse(
            requestId: "req-1",
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            statusCode: 400,
            timestamp: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var exception = new BadRequestException("bad request", context);

        var expected = string.Join('\n',
            "--- MARKET DATA SUPPORT INFO ---",
            "request_id:     req-1",
            "request_url:    https://api.marketdata.app/v1/stocks/quotes/AAPL/",
            "status_code:    400",
            "timestamp:      2025-01-15 07:00:00",
            "message:        bad request",
            "exception_type: BadRequestException",
            "--------------------------------");

        Assert.Equal(expected, exception.SupportInfo);
    }

    [Fact]
    public void SupportInfo_HasHeaderAndMatchingLengthFooter()
    {
        var context = ErrorContext.ForResponse(
            requestId: "req-1",
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            statusCode: 400,
            timestamp: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var lines = new BadRequestException("bad request", context).SupportInfo.Split('\n');

        Assert.Equal("--- MARKET DATA SUPPORT INFO ---", lines[0]);
        var footer = lines[^1];
        Assert.Equal(new string('-', lines[0].Length), footer);
        Assert.All(footer, c => Assert.Equal('-', c));
    }

    [Fact]
    public void SupportInfo_AlignsAllValuesInASingleColumn()
    {
        var context = ErrorContext.ForResponse(
            requestId: "req-1",
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            statusCode: 400,
            timestamp: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var lines = new BadRequestException("bad request", context).SupportInfo.Split('\n');

        // The longest label ("exception_type:") sets the column; values start one space past it.
        const int valueColumn = 16; // "exception_type:".Length + 1 space
        // Body lines are everything between the header and footer.
        for (var i = 1; i < lines.Length - 1; i++)
        {
            var label = SupportLabels[i - 1];
            Assert.StartsWith(label, lines[i]);
            // The label is padded so that the value always begins at the same column.
            Assert.Equal(' ', lines[i][valueColumn - 1]);
            Assert.NotEqual(' ', lines[i][valueColumn]);
        }
    }

    [Fact]
    public void SupportInfo_ShowsNone_WhenRequestIdMissing()
    {
        // ForNoResponse leaves RequestId null and StatusCode 0 (network failure).
        var context = ErrorContext.ForNoResponse(
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            timestamp: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var exception = new NetworkException("connection reset", context);

        var expected = string.Join('\n',
            "--- MARKET DATA SUPPORT INFO ---",
            "request_id:     (none)",
            "request_url:    https://api.marketdata.app/v1/stocks/quotes/AAPL/",
            "status_code:    0",
            "timestamp:      2025-01-15 07:00:00",
            "message:        connection reset",
            "exception_type: NetworkException",
            "--------------------------------");

        Assert.Equal(expected, exception.SupportInfo);
    }

    [Fact]
    public void SupportInfo_UsesRuntimeExceptionType()
    {
        var context = ErrorContext.ForResponse(
            requestId: "req-9",
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            statusCode: 429,
            timestamp: new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var exception = new RateLimitException("slow down", context);

        Assert.Contains("exception_type: RateLimitException", exception.SupportInfo);
    }

    [Fact]
    public void SupportInfo_FormatsTimestampInEastern_HonoringDaylightSaving_OnAnyPlatform()
    {
        // Regression: the US/Eastern conversion must not depend on the Windows-only
        // "Eastern Standard Time" ID, which throws on Linux/macOS. July is EDT (UTC-4),
        // so 16:00 UTC => 12:00 US/Eastern, and no offset text is rendered.
        var context = ErrorContext.ForResponse(
            requestId: "req-summer",
            requestUrl: new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            statusCode: 400,
            timestamp: new DateTimeOffset(2025, 7, 15, 16, 0, 0, TimeSpan.Zero));
        var exception = new BadRequestException("bad request", context);

        Assert.Contains("timestamp:      2025-07-15 12:00:00", exception.SupportInfo);
        Assert.DoesNotContain("-04:00", exception.SupportInfo);
    }
}

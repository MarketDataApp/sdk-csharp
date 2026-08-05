using MarketDataApp.Exceptions;

namespace MarketDataApp.Tests.Exceptions;

/// <summary>
/// Directly constructs each public exception type through every public constructor overload,
/// verifying the diagnostic-context and inner-exception plumbing. Several of these overloads (the
/// inner-exception variants and <see cref="NotFoundException"/>) are part of the public exception
/// contract but not produced by the SDK's own transport, so they are covered here.
/// </summary>
public sealed class ExceptionConstructionTests
{
    private static ErrorContext ResponseContext() => ErrorContext.ForResponse(
        "req-1",
        new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
        404,
        new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void NotFoundException_ExposesContextThroughBothConstructors()
    {
        var context = ResponseContext();
        var inner = new InvalidOperationException("cause");

        var plain = new NotFoundException("not found", context);
        Assert.Equal("not found", plain.Message);
        Assert.Equal("req-1", plain.RequestId);
        Assert.Equal(404, plain.StatusCode);
        Assert.Equal(new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"), plain.RequestUrl);
        Assert.Equal("NotFoundException", plain.ExceptionType);
        Assert.Same(context, plain.Context);
        Assert.Null(plain.InnerException);

        var wrapped = new NotFoundException("not found", context, inner);
        Assert.Same(inner, wrapped.InnerException);
    }

    [Fact]
    public void ParseException_PlainConstructor_ExposesContext()
    {
        var context = ResponseContext();
        var exception = new ParseException("bad shape", context);

        Assert.Equal("bad shape", exception.Message);
        Assert.Equal(404, exception.StatusCode);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void BadRequestAndAuthentication_InnerConstructors_PreserveCause()
    {
        var context = ResponseContext();
        var inner = new FormatException("cause");

        var badRequest = new BadRequestException("bad", context, inner);
        Assert.Same(inner, badRequest.InnerException);
        Assert.Equal("BadRequestException", badRequest.ExceptionType);

        var auth = new AuthenticationException("denied", context, inner);
        Assert.Same(inner, auth.InnerException);
        Assert.Equal("AuthenticationException", auth.ExceptionType);
    }

    [Fact]
    public void ServerException_InnerConstructor_PreservesCauseAndRetryAfter()
    {
        var context = ResponseContext();
        var inner = new HttpRequestException("cause");

        var exception = new ServerException("boom", context, inner, TimeSpan.FromSeconds(5));

        Assert.Same(inner, exception.InnerException);
        Assert.Equal(TimeSpan.FromSeconds(5), exception.RetryAfter);
    }

    [Fact]
    public void RateLimitException_InnerConstructor_PreservesCauseAndRetryAfter()
    {
        var context = ResponseContext();
        var inner = new HttpRequestException("cause");

        var exception = new RateLimitException("slow down", context, inner, TimeSpan.FromSeconds(2));

        Assert.Same(inner, exception.InnerException);
        Assert.Equal(TimeSpan.FromSeconds(2), exception.RetryAfter);
    }

    [Fact]
    public void NetworkException_NoResponseContext_HasZeroStatusAndNoRequestId()
    {
        var context = ErrorContext.ForNoResponse(
            new Uri("https://api.marketdata.app/v1/stocks/quotes/AAPL/"),
            new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var exception = new NetworkException("offline", context);

        Assert.Equal(0, exception.StatusCode);
        Assert.Null(exception.RequestId);
        Assert.Equal("NetworkException", exception.ExceptionType);
    }
}

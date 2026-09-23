using System.Globalization;
using System.Text.Json;
using MarketDataApp;

namespace MarketDataApp.Tests.Parsing;

/// <summary>
/// Direct tests for <c>JsonResponseParser</c> internals reached via <c>InternalsVisibleTo</c>:
/// the <see cref="JsonResponseParser.ParallelArrayRow"/> typed accessors (each returns
/// <see langword="null"/> for a wrong-kind element, an out-of-range number, a null element, an
/// absent field, or a field that is present but not an array), the US/Eastern reading of the
/// API's <c>dateformat=timestamp</c> shapes, and the parse-failure classifier.
/// </summary>
public sealed class JsonResponseParserInternalTests
{
    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonResponseParser.ParallelArrayRow Row(string json, int index = 0) =>
        new(Root(json), index);

    [Fact]
    public void String_ReturnsValueForStringElementAndNullOtherwise()
    {
        Assert.Equal("AAPL", Row("""{"x":["AAPL"]}""").String("x"));
        Assert.Null(Row("""{"x":[123]}""").String("x"));    // element present but not a string
        Assert.Null(Row("""{"x":[null]}""").String("x"));   // null element
        Assert.Null(Row("""{"y":["AAPL"]}""").String("x")); // field absent
    }

    [Fact]
    public void Double_ReturnsValueForNumberAndNullForNonNumber()
    {
        Assert.Equal(1.5, Row("""{"x":[1.5]}""").Double("x"));
        Assert.Null(Row("""{"x":["str"]}""").Double("x"));  // element present but not a number
    }

    [Fact]
    public void Decimal_ReturnsNullForNonNumberAndOutOfRangeNumber()
    {
        Assert.Equal(1.5m, Row("""{"x":[1.5]}""").Decimal("x"));
        Assert.Null(Row("""{"x":["str"]}""").Decimal("x")); // not a number
        Assert.Null(Row("""{"x":[1e40]}""").Decimal("x"));  // number out of decimal range
    }

    [Fact]
    public void Long_ReturnsNullForNonNumberAndNonIntegerNumber()
    {
        Assert.Equal(42L, Row("""{"x":[42]}""").Long("x"));
        Assert.Null(Row("""{"x":["str"]}""").Long("x"));    // not a number
        Assert.Null(Row("""{"x":[1.5]}""").Long("x"));      // number but not an integer
    }

    [Fact]
    public void Accessors_ReturnNullWhenFieldIsPresentButNotAnArray()
    {
        // The accessors defensively treat a present-but-non-array field as absent (returns null).
        // ReadParallelArray validates array-ness up front, so this guard is only reachable directly.
        Assert.Null(Row("""{"x":123}""").String("x"));
        Assert.Null(Row("""{"x":123}""").Double("x"));
        Assert.Null(Row("""{"x":123}""").Decimal("x"));
        Assert.Null(Row("""{"x":123}""").Long("x"));
    }

    /// <summary>
    /// Verifies that a date alone reads as midnight US/Eastern of that day, including on both
    /// daylight-saving switch days, and that a datetime keeps the instant its offset gives, or
    /// reads as UTC without one.
    /// </summary>
    /// <param name="sent">The value as the API writes it under <c>dateformat=timestamp</c>.</param>
    /// <param name="expected">The US/Eastern wall-clock time and offset the value must read as.</param>
    [Theory]
    [InlineData("2026-09-21", "2026-09-21T00:00:00-04:00")]
    [InlineData("2025-03-03", "2025-03-03T00:00:00-05:00")]
    [InlineData("2026-03-08", "2026-03-08T00:00:00-05:00")]
    [InlineData("2026-11-01", "2026-11-01T00:00:00-04:00")]
    [InlineData("2026-09-21 14:46:05 -04:00", "2026-09-21T14:46:05-04:00")]
    [InlineData("2025-03-03 09:30:00 -05:00", "2025-03-03T09:30:00-05:00")]
    [InlineData("2026-09-21 14:46:05", "2026-09-21T10:46:05-04:00")]
    public void Timestamp_ReadsTheApiTimestampShapesInUsEastern(string sent, string expected)
    {
        var timestamp = Row($$"""{"x":["{{sent}}"]}""").Timestamp("x");

        Assert.Equal(expected, timestamp?.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
    }

    /// <summary>Verifies that a timestamp element reads as null when it is neither a number nor a readable string.</summary>
    [Fact]
    public void Timestamp_ReturnsNullForAnElementThatIsNotATimestamp()
    {
        Assert.Null(Row("""{"x":[true]}""").Timestamp("x"));
        Assert.Null(Row("""{"x":[{}]}""").Timestamp("x"));
        Assert.Null(Row("""{"x":["not-a-date"]}""").Timestamp("x")); // as long as a date, but not one
    }

    [Fact]
    public void IsParseFailure_ClassifiesParseExceptionTypesAndRejectsUnrelatedOnes()
    {
        Assert.True(JsonResponseParser.IsParseFailure(new JsonException()));
        Assert.True(JsonResponseParser.IsParseFailure(new InvalidOperationException()));
        Assert.True(JsonResponseParser.IsParseFailure(new FormatException()));
        Assert.True(JsonResponseParser.IsParseFailure(new OverflowException()));
        Assert.True(JsonResponseParser.IsParseFailure(new ArgumentOutOfRangeException()));

        Assert.False(JsonResponseParser.IsParseFailure(new NotSupportedException()));
        Assert.False(JsonResponseParser.IsParseFailure(new InvalidDataException()));
    }
}

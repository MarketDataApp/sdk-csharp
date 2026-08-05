using System.Text.Json;
using MarketDataApp;

namespace MarketDataApp.Tests.Parsing;

/// <summary>
/// Direct tests for <c>JsonResponseParser</c> internals reached via <c>InternalsVisibleTo</c>:
/// the <see cref="JsonResponseParser.ParallelArrayRow"/> typed accessors (each returns
/// <see langword="null"/> for a wrong-kind element, an out-of-range number, a null element, an
/// absent field, or a field that is present but not an array) and the parse-failure classifier.
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

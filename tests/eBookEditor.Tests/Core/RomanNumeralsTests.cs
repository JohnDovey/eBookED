using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class RomanNumeralsTests
{
    [Theory]
    [InlineData(1, "i")]
    [InlineData(2, "ii")]
    [InlineData(3, "iii")]
    [InlineData(4, "iv")]
    [InlineData(5, "v")]
    [InlineData(9, "ix")]
    [InlineData(14, "xiv")]
    [InlineData(40, "xl")]
    [InlineData(99, "xcix")]
    public void ToLowerRoman_ConvertsCorrectly(int number, string expected)
    {
        Assert.Equal(expected, RomanNumerals.ToLowerRoman(number));
    }

    [Fact]
    public void ToLowerRoman_ReturnsPlainDigitsForZeroOrNegative()
    {
        Assert.Equal("0", RomanNumerals.ToLowerRoman(0));
        Assert.Equal("-1", RomanNumerals.ToLowerRoman(-1));
    }
}

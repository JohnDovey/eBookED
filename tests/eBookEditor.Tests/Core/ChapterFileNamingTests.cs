using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class ChapterFileNamingTests
{
    [Theory]
    [InlineData("23. What Now", 23, "What Now")]
    [InlineData("007 - Foo", 7, "Foo")]
    [InlineData("3_Bar", 3, "Bar")]
    [InlineData("12: Baz", 12, "Baz")]
    public void ParseHint_ExtractsLeadingNumberAndTitle(string input, int expectedNumber, string expectedTitle)
    {
        var (number, title) = ChapterFileNaming.ParseHint(input);

        Assert.Equal(expectedNumber, number);
        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void ParseHint_ReturnsNullNumberWhenNoLeadingDigits()
    {
        var (number, title) = ChapterFileNaming.ParseHint("Chapter One");

        Assert.Null(number);
        Assert.Equal("Chapter One", title);
    }

    [Fact]
    public void BuildFileName_ZeroPadsNumberToThreeDigits()
    {
        Assert.Equal("023 - What Now.md", ChapterFileNaming.BuildFileName(23, "What Now"));
    }

    [Fact]
    public void BuildFileName_OmitsNumberPrefixWhenNumberIsNull()
    {
        Assert.Equal("Untitled Draft.md", ChapterFileNaming.BuildFileName(null, "Untitled Draft"));
    }

    [Fact]
    public void BuildFileName_SanitizesInvalidFileNameCharacters()
    {
        var fileName = ChapterFileNaming.BuildFileName(1, "What? / Now:");

        Assert.DoesNotContain('?', fileName);
        Assert.DoesNotContain('/', fileName);
        Assert.DoesNotContain(':', fileName);
    }
}

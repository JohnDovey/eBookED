using eBookEditor.Core.Models;

namespace eBookEditor.Tests.Core;

public class SpineItemTests
{
    [Fact]
    public void DisplayTitle_PrefixesNumberedChapterWithItsResolvedNumber()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "What Now", ResolvedNumber = 23 };

        Assert.Equal("23. What Now", item.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_FallsBackToPlainTitleForFrontMatter()
    {
        var item = new SpineItem { Type = SpineItemType.FrontMatter, Title = "Title Page" };

        Assert.Equal("Title Page", item.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_FallsBackToPlainTitleWhenChapterHasNoResolvedNumber()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "Unnumbered", ResolvedNumber = null };

        Assert.Equal("Unnumbered", item.DisplayTitle);
    }
}

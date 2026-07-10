using eBookEditor.Core.Models;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class ChapterHeadingHtmlTests
{
    [Fact]
    public void Build_NumberedChapterWithSubtitle_ProducesH1AndH2()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "The Beginning", Subtitle = "Where it all starts", ResolvedNumber = 3 };

        var html = ChapterHeadingHtml.Build(item);

        Assert.Equal("<h1>Chapter 3: The Beginning</h1>\n<h2>Where it all starts</h2>", html);
    }

    [Fact]
    public void Build_UnnumberedChapterNoSubtitle_ProducesH1Only()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "Prologue" };

        var html = ChapterHeadingHtml.Build(item);

        Assert.Equal("<h1>Prologue</h1>", html);
    }

    [Fact]
    public void Build_TitleContainsHtmlSpecialCharacters_IsEncoded()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "Rock & Roll <3" };

        var html = ChapterHeadingHtml.Build(item);

        Assert.Contains("Rock &amp; Roll &lt;3", html!);
    }

    [Fact]
    public void Build_NonChapterItem_ReturnsNull()
    {
        var item = new SpineItem { Type = SpineItemType.FrontMatter, Title = "Title Page" };

        Assert.Null(ChapterHeadingHtml.Build(item));
    }

    [Fact]
    public void Build_ChapterWithNoTitle_ReturnsNull()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = null };

        Assert.Null(ChapterHeadingHtml.Build(item));
    }
}

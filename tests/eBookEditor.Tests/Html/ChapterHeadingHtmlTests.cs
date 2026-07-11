using eBookEditor.Core.Models;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class ChapterHeadingHtmlTests
{
    private const string BodyWithNoHeading = "<p>Once upon a time...</p>";

    [Fact]
    public void Build_NumberedChapterWithSubtitle_ProducesH1AndH2()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "The Beginning", Subtitle = "Where it all starts", ResolvedNumber = 3 };

        var html = ChapterHeadingHtml.Build(item, BodyWithNoHeading);

        Assert.Equal("<h1>Chapter 3: The Beginning</h1>\n<h2>Where it all starts</h2>", html);
    }

    [Fact]
    public void Build_UnnumberedChapterNoSubtitle_ProducesH1Only()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "Prologue" };

        var html = ChapterHeadingHtml.Build(item, BodyWithNoHeading);

        Assert.Equal("<h1>Prologue</h1>", html);
    }

    [Fact]
    public void Build_TitleContainsHtmlSpecialCharacters_IsEncoded()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "Rock & Roll <3" };

        var html = ChapterHeadingHtml.Build(item, BodyWithNoHeading);

        Assert.Contains("Rock &amp; Roll &lt;3", html!);
    }

    [Fact]
    public void Build_NonChapterItem_ReturnsNull()
    {
        var item = new SpineItem { Type = SpineItemType.FrontMatter, Title = "Title Page" };

        Assert.Null(ChapterHeadingHtml.Build(item, BodyWithNoHeading));
    }

    [Fact]
    public void Build_ChapterWithNoTitle_ReturnsNull()
    {
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = null };

        Assert.Null(ChapterHeadingHtml.Build(item, BodyWithNoHeading));
    }

    [Theory]
    [InlineData("<h1>Already Has A Heading</h1>\n<p>Text.</p>")]
    [InlineData("<h1 id=\"already-has-a-heading\">Already Has A Heading</h1>")]
    [InlineData("  \n  <h1>Indented heading, still counts</h1>")]
    public void Build_BodyAlreadyOpensWithAnH1_ReturnsNull(string body)
    {
        // Real bug, not hypothetical: some chapters are authored with their own <h1> typed
        // directly into the body (confirmed against this app's own manual/ project) — every
        // one of those got a duplicate heading before this check existed.
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "The Beginning" };

        Assert.Null(ChapterHeadingHtml.Build(item, body));
    }

    [Fact]
    public void Build_H1AppearsLaterInBodyNotAtTheStart_StillSynthesizesHeading()
    {
        // Only the body's own *opening* heading should suppress synthesis — an <h1> appearing
        // deeper in the content (e.g. inside a quoted example) isn't the chapter's own title.
        var item = new SpineItem { Type = SpineItemType.Chapter, Title = "The Beginning" };
        const string body = "<p>Some intro text.</p>\n<h1>Not the chapter title</h1>";

        var html = ChapterHeadingHtml.Build(item, body);

        Assert.Equal("<h1>The Beginning</h1>", html);
    }
}

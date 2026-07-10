using eBookEditor.Core.Services;
using eBookEditor.Migration.Services;

namespace eBookEditor.Tests.Migration;

public class MarkdownToHtmlConverterTests
{
    private readonly MarkdownToHtmlConverter _converter = new();

    [Fact]
    public void ToHtml_HeadingAttributeBlock_SetsIdAndClass()
    {
        // Markdown Extra "special attributes" ({#id .class}) — a legacy-project author could
        // have used this for in-book navigation before the HTML content-model refactor; the
        // migration converter must still parse it correctly.
        const string markdown = "## Header 2 {.myclass #header2 lang=fr}";

        var html = _converter.ToHtml(markdown);

        Assert.Contains("id=\"header2\"", html);
        Assert.Contains("class=\"myclass\"", html);
        Assert.Contains("lang=\"fr\"", html);
    }

    [Fact]
    public void ToHtml_SamePageLinkToHeadingId_ProducesAWorkingFragmentLink()
    {
        const string markdown = """
            # Header 1 {#header1}

            [Link back to header 1](#header1)
            """;

        var html = _converter.ToHtml(markdown);

        Assert.Contains("id=\"header1\"", html);
        Assert.Contains("href=\"#header1\"", html);
    }

    [Fact]
    public void ToHtml_CustomContainer_MatchesTheLegacyApplyStyleShape()
    {
        // Before the HTML content-model refactor, MainWindow.OnApplyStyleClick wrapped a
        // selection as "::: {.class}\n<text>\n:::" — a legacy project's ".md" files can still
        // contain this shape, and the migration converter must still parse it correctly, for
        // every style in the catalog the menu used to offer.
        foreach (var style in EditorStyleCatalog.Styles)
        {
            var markdown = $"::: {{.{style.ClassName}}}\nSome styled text.\n:::";

            var html = _converter.ToHtml(markdown);

            Assert.Contains($"class=\"{style.ClassName}\"", html);
            Assert.Contains("Some styled text.", html);
        }
    }

    [Fact]
    public void ToHtml_InsertImageContainerShape_RendersImageAndClassHookedCaption()
    {
        // Before the HTML content-model refactor, MainWindow.OnInsertImageClick wrapped an
        // inserted image exactly like this — a classless outer container (just a grouping
        // <div>) holding the image and an inner ".caption"-classed container for the caption
        // text. This is deliberately NOT a Markdown table: a trailing "{.class}" attribute
        // block immediately after a table makes Markdig fail to recognize the table at all
        // (falls back to literal pipe-character text) — verified directly against the
        // pipeline, not documented anywhere. The outer fence must use more colons than the
        // inner one, or Markdig emits a stray empty trailing <div></div> — also verified
        // directly, not documented. A legacy project's ".md" files can still contain this
        // shape, and the migration converter must still parse it correctly.
        const string markdown = """
            ::::
            ![A photo](../images/photo.jpg)

            ::: {.caption}
            Caption text
            :::
            ::::
            """;

        var html = _converter.ToHtml(markdown);

        Assert.Contains("<img src=\"../images/photo.jpg\"", html);
        Assert.Contains("class=\"caption\"", html);
        Assert.Contains("Caption text", html);
        Assert.DoesNotContain("<div></div>", html);
    }
}

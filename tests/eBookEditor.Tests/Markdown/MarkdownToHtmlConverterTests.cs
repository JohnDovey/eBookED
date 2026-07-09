using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Markdown;

public class MarkdownToHtmlConverterTests
{
    private readonly MarkdownToHtmlConverter _converter = new();

    [Fact]
    public void ToHtml_HeadingAttributeBlock_SetsIdAndClass()
    {
        // Markdown Extra "special attributes" ({#id .class}) — used both for the manual's
        // own documented syntax and, via Style ids, for editor-inserted heading anchors.
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
    public void ToHtml_CustomContainer_MatchesTheStringFormatTheApplyStyleMenuInserts()
    {
        // MainWindow.OnApplyStyleClick wraps a selection as "::: {.class}\n<text>\n:::" —
        // verify that exact shape parses to a real, class-hooked <div>, for every style in
        // the catalog the menu actually offers.
        foreach (var style in EditorStyleCatalog.Styles)
        {
            var markdown = $"::: {{.{style.ClassName}}}\nSome styled text.\n:::";

            var html = _converter.ToHtml(markdown);

            Assert.Contains($"class=\"{style.ClassName}\"", html);
            Assert.Contains("Some styled text.", html);
        }
    }

}

using AngleSharp.Html.Parser;
using eBookEditor.EpubImport.Services;

namespace eBookEditor.Tests.EpubImport;

public class EpubFootnoteConverterTests
{
    private static readonly HtmlParser Parser = new();

    private static (AngleSharp.Dom.IDocument Document, AngleSharp.Dom.IElement Body) ParseBody(string bodyHtml)
    {
        var document = Parser.ParseDocument($"<!DOCTYPE html><html><body>{bodyHtml}</body></html>");
        return (document, document.Body!);
    }

    [Fact]
    public void RewriteFootnotes_PlainSamePageShape_ConvertsToThisAppsFormat()
    {
        const string html = """
            <p>A sentence with a note.<sup><a href="#note1">1</a></sup></p>
            <p id="note1">The note text.</p>
            """;
        var (_, body) = ParseBody(html);

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(0, todoCount);
        Assert.Contains(body.QuerySelectorAll("sup"), s => s.Id == "fnref:1");
        var refAnchor = body.QuerySelector("sup a.footnote-ref");
        Assert.NotNull(refAnchor);
        Assert.Equal("#fn:1", refAnchor!.GetAttribute("href"));

        var footnotesDiv = body.QuerySelector("div.footnotes");
        Assert.NotNull(footnotesDiv);
        var li = footnotesDiv!.QuerySelector("li#fn\\:1");
        Assert.NotNull(li);
        Assert.Contains("The note text.", li!.TextContent);
        Assert.NotNull(li.QuerySelector("a.footnote-back-ref"));

        // The original standalone definition paragraph should have been removed from its old
        // position, not left duplicated in the main flow.
        Assert.Null(body.QuerySelector("#note1"));
    }

    [Fact]
    public void RewriteFootnotes_Epub3NativeShape_ConvertsToThisAppsFormat()
    {
        const string html = """
            <p>A sentence with a note.<a epub:type="noteref" href="#fn1">1</a></p>
            <aside epub:type="footnote" id="fn1"><p>The note text.</p></aside>
            """;
        var (_, body) = ParseBody(html);

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(0, todoCount);
        Assert.NotNull(body.QuerySelector("sup#fnref\\:1"));
        var li = body.QuerySelector("div.footnotes li#fn\\:1");
        Assert.NotNull(li);
        Assert.Contains("The note text.", li!.TextContent);
    }

    [Fact]
    public void RewriteFootnotes_ReferenceTargetingAMissingId_InsertsATodoFootnoteAndLeavesOriginalUntouched()
    {
        const string html = """<p>A sentence with a note.<sup><a href="#doesnotexist">1</a></sup></p>""";
        var (_, body) = ParseBody(html);

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(1, todoCount);
        // Original unresolved reference stays exactly as it was.
        Assert.Contains(body.QuerySelectorAll("a"), a => a.GetAttribute("href") == "#doesnotexist");
        // A new, real footnote was inserted alongside it.
        var todoLi = body.QuerySelector("div.footnotes li");
        Assert.NotNull(todoLi);
        Assert.Contains("TODO", todoLi!.TextContent);
        Assert.Contains("review", todoLi.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RewriteFootnotes_TargetBeforeReference_IsTreatedAsUnresolvable()
    {
        // A definition can't precede its own reference in normal usage — this id match is
        // almost certainly coincidental, not a real footnote pairing.
        const string html = """
            <p id="note1">Some earlier paragraph, not a footnote definition.</p>
            <p>A sentence with a note.<sup><a href="#note1">1</a></sup></p>
            """;
        var (_, body) = ParseBody(html);

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(1, todoCount);
    }

    [Fact]
    public void RewriteFootnotes_NoFootnoteLikeMarkup_IsANoOp()
    {
        const string html = "<p>Just an ordinary paragraph with no notes.</p>";
        var (_, body) = ParseBody(html);
        var originalHtml = body.InnerHtml;

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(0, todoCount);
        Assert.Equal(originalHtml, body.InnerHtml);
    }

    [Fact]
    public void RewriteFootnotes_MultipleFootnotes_NumbersThemSequentially()
    {
        const string html = """
            <p>First note.<sup><a href="#n1">1</a></sup> Second note.<sup><a href="#n2">2</a></sup></p>
            <p id="n1">First note text.</p>
            <p id="n2">Second note text.</p>
            """;
        var (_, body) = ParseBody(html);

        var todoCount = EpubFootnoteConverter.RewriteFootnotes(body);

        Assert.Equal(0, todoCount);
        Assert.NotNull(body.QuerySelector("sup#fnref\\:1"));
        Assert.NotNull(body.QuerySelector("sup#fnref\\:2"));
        var listItems = body.QuerySelectorAll("div.footnotes li");
        Assert.Equal(2, listItems.Length);
    }
}

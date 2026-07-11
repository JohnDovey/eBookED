using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class SameDocumentLinkConverterTests
{
    [Fact]
    public void Convert_ResolvableFragment_RenamesTheTargetIdAndRewritesTheHref()
    {
        const string html = """<p>See <a href="#note">the note</a>.</p><p id="note">Here it is.</p>""";

        var result = SameDocumentLinkConverter.Convert(html);

        Assert.Contains("id=\"dest:note\"", result);
        Assert.Contains("href=\"#dest:note\"", result);
        Assert.DoesNotContain("id=\"note\"", result);
    }

    [Fact]
    public void Convert_UnresolvableFragment_LeavesTheHtmlUntouched()
    {
        const string html = """<p>See <a href="#missing">nowhere</a>.</p>""";

        var result = SameDocumentLinkConverter.Convert(html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Convert_FootnoteFragments_AreNeverConverted()
    {
        const string html = """
            <p>Note.<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup></p>
            <div class="footnotes"><ol><li id="fn:1"><p>The note. <a href="#fnref:1" class="footnote-back-ref">&#8617;</a></p></li></ol></div>
            """;

        var result = SameDocumentLinkConverter.Convert(html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Convert_TwoFragmentsThatSlugifyToTheSameValue_GetDisambiguated()
    {
        // "Note" and "note" are distinct real ids (HTML ids are case-sensitive) but both
        // normalize to the slug "note" — the second one must not silently overwrite the first.
        const string html = """
            <p><a href="#Note">one</a> <a href="#note">two</a></p>
            <p id="Note">First.</p>
            <p id="note">Second.</p>
            """;

        var result = SameDocumentLinkConverter.Convert(html);

        Assert.Contains("href=\"#dest:note\"", result);
        Assert.Contains("href=\"#dest:note-2\"", result);
    }

    [Fact]
    public void Convert_NoFragmentLinks_ReturnsTheSameHtmlUnchanged()
    {
        const string html = """<p>Plain text with <a href="https://example.com">an external link</a>.</p>""";

        var result = SameDocumentLinkConverter.Convert(html);

        Assert.Equal(html, result);
    }
}

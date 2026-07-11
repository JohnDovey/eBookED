using eBookEditor.EpubImport.Services;

namespace eBookEditor.Tests.EpubImport;

public class EpubInternalHrefRewriterTests
{
    [Fact]
    public void Rewrite_MatchingHref_IsRewrittenToTheMappedRelativePath()
    {
        const string html = """<p>See <a href="chapter3.xhtml">chapter three</a> for more.</p>""";
        var map = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };

        var result = EpubInternalHrefRewriter.Rewrite(html, map);

        Assert.Contains("href=\"chapters/003-Chapter-Three.ebhtml\"", result);
    }

    [Fact]
    public void Rewrite_HrefWithFragment_DropsTheFragmentAndUsesTheMappedPath()
    {
        const string html = """<a href="chapter3.xhtml#section2">jump</a>""";
        var map = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };

        var result = EpubInternalHrefRewriter.Rewrite(html, map);

        Assert.Contains("href=\"chapters/003-Chapter-Three.ebhtml\"", result);
        Assert.DoesNotContain("#section2", result);
    }

    [Fact]
    public void Rewrite_NonMatchingHref_IsLeftUntouched()
    {
        const string html = """<a href="https://example.com/">external</a>""";
        var map = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };

        var result = EpubInternalHrefRewriter.Rewrite(html, map);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Rewrite_EmptyMap_LeavesHtmlUntouched()
    {
        const string html = """<a href="chapter3.xhtml">chapter three</a>""";

        var result = EpubInternalHrefRewriter.Rewrite(html, new Dictionary<string, string>());

        Assert.Equal(html, result);
    }

    [Fact]
    public void Rewrite_PreservesOtherAttributesOnTheAnchor()
    {
        const string html = """<a class="ref" href="chapter3.xhtml" title="See also">link</a>""";
        var map = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };

        var result = EpubInternalHrefRewriter.Rewrite(html, map);

        Assert.Contains("class=\"ref\"", result);
        Assert.Contains("title=\"See also\"", result);
    }

    [Fact]
    public void FindFragmentReferences_FindsCrossChapterAndSameChapterFragments_AndSkipsFragmentlessLinks()
    {
        const string html = """
            <a href="chapter3.xhtml#section2">cross-chapter</a>
            <a href="#local">same-chapter</a>
            <a href="chapter4.xhtml">no fragment</a>
            """;

        var references = EpubInternalHrefRewriter.FindFragmentReferences(html);

        Assert.Contains(("chapter3.xhtml", "section2"), references);
        Assert.Contains(("", "local"), references);
        Assert.DoesNotContain(references, r => r.TargetHref == "chapter4.xhtml");
    }

    [Fact]
    public void HasId_FindsAMatchingIdRegardlessOfQuoteStyle()
    {
        const string html = """<h2 id="section2">Heading</h2><p id='other'>Text</p>""";

        Assert.True(EpubInternalHrefRewriter.HasId(html, "section2"));
        Assert.True(EpubInternalHrefRewriter.HasId(html, "other"));
        Assert.False(EpubInternalHrefRewriter.HasId(html, "missing"));
    }

    [Fact]
    public void RenameIds_RetargetsOnlyTheMatchingIds()
    {
        const string html = """<h2 id="section2">Heading</h2><p id="unrelated">Text</p>""";
        var map = new Dictionary<string, string> { ["section2"] = "dest:section2" };

        var result = EpubInternalHrefRewriter.RenameIds(html, map);

        Assert.Contains("id=\"dest:section2\"", result);
        Assert.Contains("id=\"unrelated\"", result);
    }

    [Fact]
    public void RewriteHrefsWithFragments_ResolvedFragment_UsesTheDestId()
    {
        const string html = """<a href="chapter3.xhtml#section2">jump</a>""";
        var pathMap = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };
        var fragmentDestIds = new Dictionary<(string, string), string> { [("chapter3.xhtml", "section2")] = "dest:section2" };

        var result = EpubInternalHrefRewriter.RewriteHrefsWithFragments(html, "chapter1.xhtml", pathMap, fragmentDestIds);

        Assert.Contains("href=\"chapters/003-Chapter-Three.ebhtml#dest:section2\"", result);
    }

    [Fact]
    public void RewriteHrefsWithFragments_UnresolvedFragment_FallsBackToTheChapterLevelPath()
    {
        const string html = """<a href="chapter3.xhtml#missing">jump</a>""";
        var pathMap = new Dictionary<string, string> { ["chapter3.xhtml"] = "chapters/003-Chapter-Three.ebhtml" };
        var fragmentDestIds = new Dictionary<(string, string), string>();

        var result = EpubInternalHrefRewriter.RewriteHrefsWithFragments(html, "chapter1.xhtml", pathMap, fragmentDestIds);

        Assert.Contains("href=\"chapters/003-Chapter-Three.ebhtml\"", result);
        Assert.DoesNotContain("#", result);
    }

    [Fact]
    public void RewriteHrefsWithFragments_SameChapterFragment_ResolvesAgainstTheCurrentChapter()
    {
        const string html = """<a href="#local">jump</a>""";
        var pathMap = new Dictionary<string, string> { ["chapter1.xhtml"] = "chapters/001-Chapter-One.ebhtml" };
        var fragmentDestIds = new Dictionary<(string, string), string> { [("chapter1.xhtml", "local")] = "dest:local" };

        var result = EpubInternalHrefRewriter.RewriteHrefsWithFragments(html, "chapter1.xhtml", pathMap, fragmentDestIds);

        Assert.Contains("href=\"chapters/001-Chapter-One.ebhtml#dest:local\"", result);
    }
}

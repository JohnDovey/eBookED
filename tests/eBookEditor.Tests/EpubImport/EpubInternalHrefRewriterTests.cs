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
}

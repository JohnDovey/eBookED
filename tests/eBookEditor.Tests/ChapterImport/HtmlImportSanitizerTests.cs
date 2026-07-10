using eBookEditor.ChapterImport.Services;

namespace eBookEditor.Tests.ChapterImport;

public class HtmlImportSanitizerTests
{
    private readonly HtmlImportSanitizer _sanitizer = new();

    [Fact]
    public void Convert_PassesThroughBasicFormattingUnchanged()
    {
        var html = _sanitizer.Convert("<p>Hello <strong>world</strong>, this is <em>italic</em>.</p>");

        Assert.Contains("<strong>world</strong>", html);
        Assert.Contains("<em>italic</em>", html);
    }

    [Fact]
    public void Convert_PassesThroughHeadingsAndLists()
    {
        var html = _sanitizer.Convert("<h1>Title</h1><ul><li>One</li><li>Two</li></ul>");

        Assert.Contains("<h1>Title</h1>", html);
        Assert.Contains("<li>One</li>", html);
        Assert.Contains("<li>Two</li>", html);
    }

    [Fact]
    public void Convert_StripsScriptAndStyleElementsEntirely()
    {
        var html = _sanitizer.Convert("<p>Hi</p><script>alert('x')</script><style>p { color: red }</style>");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>Hi</p>", html);
    }

    [Fact]
    public void Convert_StripsInlineEventHandlerAttributes()
    {
        var html = _sanitizer.Convert("<p onclick=\"doEvil()\">Hi</p>");

        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hi", html);
    }

    [Fact]
    public void Convert_StripsJavascriptUrlsFromHrefAndSrc()
    {
        var html = _sanitizer.Convert("<a href=\"javascript:doEvil()\">Link</a><img src=\"javascript:doEvil()\">");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Convert_DropsHeadContentButKeepsBody()
    {
        var html = _sanitizer.Convert("<html><head><title>Ignored</title></head><body><p>Real content.</p></body></html>");

        Assert.DoesNotContain("Ignored", html);
        Assert.Contains("Real content.", html);
    }

    [Fact]
    public void Convert_PreservesOrdinaryHrefAndSrcAttributes()
    {
        var html = _sanitizer.Convert("<a href=\"https://example.com/\">Link</a><img src=\"images/photo.jpg\">");

        Assert.Contains("href=\"https://example.com/\"", html);
        Assert.Contains("src=\"images/photo.jpg\"", html);
    }
}

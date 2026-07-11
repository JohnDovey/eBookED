using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class HtmlPageShellTests
{
    [Fact]
    public void Wrap_EmbedsCssAndBody()
    {
        var html = HtmlPageShell.Wrap("body { color: red; }", "<p>Hello</p>", editable: false);

        Assert.Contains("<style>body { color: red; }</style>", html);
        Assert.Contains("<p>Hello</p>", html);
    }

    [Fact]
    public void Wrap_NotEditable_HasNoContentEditableAttribute()
    {
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: false);

        Assert.DoesNotContain("contenteditable", html);
    }

    [Fact]
    public void Wrap_Editable_MarksContentElementContentEditable()
    {
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: true);

        Assert.Contains($"id=\"{HtmlPageShell.ContentElementId}\" contenteditable=\"true\"", html);
    }

    [Fact]
    public void Wrap_WithHeading_RendersHeadingOutsideTheContentElement()
    {
        var html = HtmlPageShell.Wrap("", "<p>Body text</p>", editable: true, headingHtml: "<h1>Chapter 1: Title</h1>");

        var headingIndex = html.IndexOf("<h1>Chapter 1: Title</h1>", StringComparison.Ordinal);
        var contentDivIndex = html.IndexOf($"id=\"{HtmlPageShell.ContentElementId}\"", StringComparison.Ordinal);

        Assert.True(headingIndex >= 0 && headingIndex < contentDivIndex,
            "the heading must appear before the #content element, not inside it");
    }

    [Fact]
    public void Wrap_NoHeading_OmitsHeadingMarkup()
    {
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: false);

        Assert.DoesNotContain("<h1>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Wrap_AlwaysIncludesTheJsBridge(bool editable)
    {
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable);

        Assert.Contains("window.ebookEditor", html);
        Assert.Contains("insertHtml", html);
        Assert.Contains("wrapSelection", html);
        Assert.Contains("scrollToFraction", html);
        Assert.Contains("appendFootnoteDefinition", html);
    }

    [Fact]
    public void BuildFileBaseUri_WithAProjectDirectory_ReturnsAFileUriWithTrailingSeparator()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var uri = HtmlPageShell.BuildFileBaseUri(tempDir);

            Assert.Equal("file", uri.Scheme);
            Assert.EndsWith("/", uri.AbsoluteUri);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void BuildFileBaseUri_ResolvesARelativeImagePathAgainstTheProjectDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseUri = HtmlPageShell.BuildFileBaseUri(tempDir);

            var resolved = new Uri(baseUri, "../images/foo.jpg");

            Assert.Equal(Path.Combine(Path.GetDirectoryName(tempDir)!, "images", "foo.jpg"), resolved.LocalPath);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildFileBaseUri_NoDirectory_FallsBackToAboutBlank(string? directory)
    {
        var uri = HtmlPageShell.BuildFileBaseUri(directory);

        Assert.Equal("about:blank", uri.ToString());
    }
}

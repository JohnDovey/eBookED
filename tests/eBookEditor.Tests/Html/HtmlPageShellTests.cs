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
        Assert.Contains("wrapSelectionWithId", html);
        Assert.Contains("insertOrWrapLink", html);
        Assert.Contains("wrapSelectionAsIndexEntry", html);
        Assert.Contains("markAllOccurrences", html);
        Assert.Contains("scrollToFraction", html);
        Assert.Contains("appendFootnoteDefinition", html);
        Assert.Contains("deleteSelection", html);
        Assert.Contains("getSelectionHtml", html);
    }

    [Fact]
    public void Wrap_DeleteSelection_RemovesTheNearestBlockAncestorWhenCollapsed()
    {
        // Regression test: a collapsed selection (a bare caret, no highlighted text) has nothing
        // for range.deleteContents() to remove, so Delete needs to walk up to the nearest
        // block-level ancestor (figure/table/list item/paragraph/heading/etc.) and remove that
        // whole element instead — otherwise clicking Delete with just a caret in a figure's
        // caption would silently do nothing.
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: true);

        Assert.Contains("FIGURE: 1", html);
        Assert.Contains("node.remove()", html);
    }

    [Fact]
    public void Wrap_PostsAReadyEventOnceTheBridgeItselfIsCallable()
    {
        // Regression test: toolbar commands (Bold/Italic/Insert Element/Apply Style/etc.) all
        // gate on MainWindow's _wysiwygNavigated flag before invoking window.ebookEditor.* —
        // this "ready" message is what actually sets it, since the WebView's own navigation-
        // completed signal isn't a safe proxy for "this script has finished running and
        // window.ebookEditor now exists" (see OnWysiwygMessageBody's own doc comment).
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: true);

        Assert.Contains("{ event: 'ready' }", html);

        var readyIndex = html.IndexOf("event: 'ready'", StringComparison.Ordinal);
        var ebookEditorIndex = html.IndexOf("window.ebookEditor = {", StringComparison.Ordinal);
        Assert.True(readyIndex > ebookEditorIndex,
            "the ready ping must fire after window.ebookEditor is assigned, not before");
    }

    [Fact]
    public void WritePreviewFile_WritesTheHtmlUnderAHiddenDirectoryOneLevelBelowTheProjectRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var uri = HtmlPageShell.WritePreviewFile(tempDir, "<p>Hello</p>");

            Assert.Equal("file", uri.Scheme);
            var expectedPath = Path.Combine(tempDir, ".eb-preview", "preview.html");
            Assert.Equal(Path.GetFullPath(expectedPath), uri.LocalPath);
            Assert.True(File.Exists(uri.LocalPath));
            Assert.Equal("<p>Hello</p>", File.ReadAllText(uri.LocalPath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WritePreviewFile_ResolvesARelativeImagePathAgainstTheProjectDirectory()
    {
        // The whole point of writing a real file one level below the project root (rather than
        // navigating via NavigateToString's baseUri workaround) — a stored body's "../images/
        // foo.jpg" convention must resolve to the same place a real front/back-matter or
        // chapter file's own "../images/foo.jpg" would.
        var tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var uri = HtmlPageShell.WritePreviewFile(tempDir, "<p>Hello</p>");

            var resolved = new Uri(uri, "../images/foo.jpg");

            Assert.Equal(Path.Combine(tempDir, "images", "foo.jpg"), resolved.LocalPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WritePreviewFile_CalledTwice_OverwritesRatherThanAccumulatingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            HtmlPageShell.WritePreviewFile(tempDir, "<p>First</p>");
            var uri = HtmlPageShell.WritePreviewFile(tempDir, "<p>Second</p>");

            Assert.Equal("<p>Second</p>", File.ReadAllText(uri.LocalPath));
            Assert.Single(Directory.GetFiles(Path.Combine(tempDir, ".eb-preview")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

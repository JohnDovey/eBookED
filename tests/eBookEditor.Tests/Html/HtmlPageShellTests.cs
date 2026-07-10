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
    public void Wrap_Editable_MarksBodyContentEditable()
    {
        var html = HtmlPageShell.Wrap("", "<p>Hello</p>", editable: true);

        Assert.Contains($"id=\"{HtmlPageShell.ContentElementId}\" contenteditable=\"true\"", html);
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
}

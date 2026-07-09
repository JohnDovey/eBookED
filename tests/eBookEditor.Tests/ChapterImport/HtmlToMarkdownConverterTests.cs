using eBookEditor.ChapterImport.Services;

namespace eBookEditor.Tests.ChapterImport;

public class HtmlToMarkdownConverterTests
{
    private readonly HtmlToMarkdownConverter _converter = new();

    [Fact]
    public void Convert_TranslatesBasicFormattingToMarkdown()
    {
        var markdown = _converter.Convert("<p>Hello <strong>world</strong>, this is <em>italic</em>.</p>");

        Assert.Contains("**world**", markdown);
        Assert.Contains("*italic*", markdown);
    }

    [Fact]
    public void Convert_TranslatesHeadingsAndLists()
    {
        var markdown = _converter.Convert("<h1>Title</h1><ul><li>One</li><li>Two</li></ul>");

        Assert.Contains("# Title", markdown);
        Assert.Contains("One", markdown);
        Assert.Contains("Two", markdown);
    }
}

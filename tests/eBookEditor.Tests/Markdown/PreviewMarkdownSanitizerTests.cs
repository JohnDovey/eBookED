using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Markdown;

public class PreviewMarkdownSanitizerTests
{
    [Fact]
    public void Sanitize_RemovesCustomContainerFenceLines_KeepingTheInnerContent()
    {
        const string markdown = "::: {.smallcaps}\nStyled paragraph.\n:::";

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.DoesNotContain(":::", result);
        Assert.Contains("Styled paragraph.", result);
    }

    [Fact]
    public void Sanitize_RemovesNestedContainerFencesOfDifferentLengths()
    {
        const string markdown = """
            ::::
            ![A photo](../images/photo.jpg)

            ::: {.caption}
            Caption text
            :::
            ::::
            """;

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.DoesNotContain(":", result);
        Assert.Contains("![A photo](../images/photo.jpg)", result);
        Assert.Contains("Caption text", result);
    }

    [Fact]
    public void Sanitize_StripsAttributeBlockGluedToAHeading()
    {
        const string markdown = "## Header 2 {.myclass #header2 lang=fr}";

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.Equal("## Header 2", result);
    }

    [Fact]
    public void Sanitize_StripsAttributeBlockGluedToALink()
    {
        const string markdown = "[link](url){#id .class}";

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.Equal("[link](url)", result);
    }

    [Fact]
    public void Sanitize_StripsStandaloneAttributeLineAfterATable()
    {
        const string markdown = "| A |\n| --- |\n| 1 |\n{.caption}";

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.DoesNotContain("{.caption}", result);
        Assert.Contains("| A |", result);
    }

    [Fact]
    public void Sanitize_DoesNotTouchUnrelatedTrailingBraces()
    {
        const string markdown = "Some inline code: `foo() {}`";

        var result = PreviewMarkdownSanitizer.Sanitize(markdown);

        Assert.Equal(markdown, result);
    }
}

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Services;

namespace eBookEditor.Tests.DocxImport;

public class MarkdownToDocxConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MarkdownToDocxConverter _converter = new();

    public MarkdownToDocxConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ConvertToFile_ProducesDocxWithTitleHeadingsFormattingAndLists()
    {
        const string markdown = """
            Hello **bold** and *italic* text.

            ## Subheading

            - First item
            - Second item

            1. Step one
            2. Step two
            """;
        var path = Path.Combine(_tempDir, "chapter.docx");

        _converter.ConvertToFile(markdown, "Chapter One", path);

        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        Assert.Equal("Chapter One", paragraphs[0].InnerText);
        Assert.Equal("Title", paragraphs[0].ParagraphProperties!.ParagraphStyleId!.Val!.Value);

        var boldRun = paragraphs.SelectMany(p => p.Descendants<Run>()).FirstOrDefault(r => r.RunProperties?.Bold is not null);
        Assert.NotNull(boldRun);
        Assert.Contains("bold", boldRun!.InnerText);

        var italicRun = paragraphs.SelectMany(p => p.Descendants<Run>()).FirstOrDefault(r => r.RunProperties?.Italic is not null);
        Assert.NotNull(italicRun);
        Assert.Contains("italic", italicRun!.InnerText);

        Assert.Contains(paragraphs, p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading2" && p.InnerText == "Subheading");
        Assert.Contains(paragraphs, p => p.InnerText == "• First item");
        Assert.Contains(paragraphs, p => p.InnerText == "1. Step one");
    }

    [Fact]
    public void ConvertToFile_RendersLinksAsHyperlinks()
    {
        var path = Path.Combine(_tempDir, "chapter-link.docx");

        _converter.ConvertToFile("Visit [our site](https://example.com) for more.", "Chapter With Link", path);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        var hyperlink = mainPart.Document!.Body!.Descendants<Hyperlink>().Single();

        Assert.Equal("our site", hyperlink.InnerText);
        var relationship = mainPart.HyperlinkRelationships.Single(r => r.Id == hyperlink.Id);
        Assert.Equal("https://example.com/", relationship.Uri.ToString());
    }
}

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

    [Fact]
    public void ConvertToFile_RendersTables()
    {
        const string markdown = """
            | Name | Role |
            | --- | --- |
            | Jane Doe | Author |
            | Ed Itor | Editor |
            """;
        var path = Path.Combine(_tempDir, "chapter-table.docx");

        _converter.ConvertToFile(markdown, "Chapter With Table", path);

        using var document = WordprocessingDocument.Open(path, false);
        var table = document.MainDocumentPart!.Document!.Body!.Descendants<Table>().Single();
        var rows = table.Elements<TableRow>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Name", rows[0].Elements<TableCell>().First().InnerText);
        Assert.Equal("Jane Doe", rows[1].Elements<TableCell>().First().InnerText);
        Assert.Equal("Editor", rows[2].Elements<TableCell>().ElementAt(1).InnerText);
    }

    [Fact]
    public void ConvertToFile_EmbedsImagesResolvedAgainstSourceDir()
    {
        // Minimal valid 1x1 PNG.
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var imagesDir = Path.Combine(_tempDir, "images");
        Directory.CreateDirectory(imagesDir);
        File.WriteAllBytes(Path.Combine(imagesDir, "cover.png"), pngBytes);

        var chaptersDir = Path.Combine(_tempDir, "chapters");
        Directory.CreateDirectory(chaptersDir);
        var path = Path.Combine(_tempDir, "chapter-image.docx");

        _converter.ConvertToFile("![Cover](../images/cover.png)", "Chapter With Image", path, chaptersDir);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        Assert.Single(mainPart.ImageParts);
        Assert.NotNull(mainPart.Document!.Body!.Descendants<Drawing>().SingleOrDefault());
    }

    [Fact]
    public void ConvertToFile_MissingImageIsSkippedWithoutError()
    {
        var path = Path.Combine(_tempDir, "chapter-missing-image.docx");

        _converter.ConvertToFile("![Missing](../images/does-not-exist.png)", "Chapter", path, _tempDir);

        using var document = WordprocessingDocument.Open(path, false);
        Assert.Empty(document.MainDocumentPart!.ImageParts);
    }

    [Fact]
    public void ConvertToFile_AddsRealWordFootnotes()
    {
        const string markdown = """
            Some text with a footnote[^1].

            [^1]: This is the note.
            """;
        var path = Path.Combine(_tempDir, "chapter-footnote.docx");

        _converter.ConvertToFile(markdown, "Chapter With Footnote", path);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        var footnoteReference = mainPart.Document!.Body!.Descendants<FootnoteReference>().Single();
        Assert.Equal(1, footnoteReference.Id!.Value);

        var footnotesPart = mainPart.FootnotesPart;
        Assert.NotNull(footnotesPart);
        var realFootnote = footnotesPart!.Footnotes!.Elements<Footnote>().Single(f => f.Id!.Value == 1);
        Assert.Contains("This is the note.", realFootnote.InnerText);
    }

    [Fact]
    public void ConvertToFile_RendersFencedCodeBlocksAsMonospaceParagraphs()
    {
        const string markdown = """
            ```
            def greet(name):
                return f"Hello, {name}!"
            ```
            """;
        var path = Path.Combine(_tempDir, "chapter-code.docx");

        _converter.ConvertToFile(markdown, "Chapter With Code", path);

        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        var codeParagraph = paragraphs.Single(p => p.InnerText.Contains("def greet"));

        var runs = codeParagraph.Elements<Run>().ToList();
        // One text run per line, plus a Break run between them (2 lines -> 3 runs).
        Assert.Equal(3, runs.Count);
        Assert.NotNull(runs[1].Elements<Break>().SingleOrDefault());
        Assert.Equal("Courier New", runs[0].RunProperties!.RunFonts!.Ascii);
        Assert.Equal("Courier New", runs[2].RunProperties!.RunFonts!.Ascii);
        Assert.Contains("def greet(name):", codeParagraph.InnerText);
        Assert.Contains("return f\"Hello, {name}!\"", codeParagraph.InnerText);
    }

    [Fact]
    public void ConvertToFile_RendersThematicBreaksAsPageBreaks()
    {
        // MarkdownExportService.ExportWholeBook separates sections with "---"; the whole-book
        // Word export relies on this becoming a page break so each section still starts on
        // its own page, matching the EPUB/PDF exports' convention.
        const string markdown = """
            First section.

            ---

            Second section.
            """;
        var path = Path.Combine(_tempDir, "chapter-thematic-break.docx");

        _converter.ConvertToFile(markdown, "Chapter With Break", path);

        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var pageBreak = body.Descendants<Break>().SingleOrDefault(b => b.Type?.Value == BreakValues.Page);

        Assert.NotNull(pageBreak);
    }

    [Fact]
    public void ConvertToFile_RendersEmphasisExtrasCorrectly_NotAsBoldOrItalic()
    {
        // Regression test: strikethrough (~~), highlight (==), subscript (~), superscript (^),
        // and inserted (++) all parse as EmphasisInline distinguished only by DelimiterChar —
        // a version of this code that checked only DelimiterCount misread every one of them
        // as bold or italic instead.
        const string markdown = "~~struck~~ ==marked== H~2~O E=mc^2^ ++inserted++";
        var path = Path.Combine(_tempDir, "chapter-emphasis-extras.docx");

        _converter.ConvertToFile(markdown, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var runs = document.MainDocumentPart!.Document!.Body!.Descendants<Run>()
            .Where(r => !string.IsNullOrEmpty(r.InnerText))
            .ToList();

        var struckRun = runs.Single(r => r.InnerText == "struck");
        Assert.NotNull(struckRun.RunProperties!.Strike);
        Assert.Null(struckRun.RunProperties.Bold);

        var markedRun = runs.Single(r => r.InnerText == "marked");
        Assert.Equal(HighlightColorValues.Yellow, markedRun.RunProperties!.Highlight!.Val!.Value);
        Assert.Null(markedRun.RunProperties.Bold);

        var twoRuns = runs.Where(r => r.InnerText == "2").ToList();
        Assert.Contains(twoRuns, r => r.RunProperties?.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Subscript);
        Assert.Contains(twoRuns, r => r.RunProperties?.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Superscript);
        Assert.All(twoRuns, r => Assert.Null(r.RunProperties!.Italic));

        var insertedRun = runs.Single(r => r.InnerText == "inserted");
        Assert.Equal(UnderlineValues.Single, insertedRun.RunProperties!.Underline!.Val!.Value);
        Assert.Null(insertedRun.RunProperties.Bold);
    }

    [Fact]
    public void ConvertToFile_RendersDefinitionLists()
    {
        const string markdown = "Apple\n:   Pomaceous fruit\n:   Comes in many colors\n";
        var path = Path.Combine(_tempDir, "chapter-definition-list.docx");

        _converter.ConvertToFile(markdown, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

        var termParagraph = paragraphs.Single(p => p.InnerText == "Apple");
        Assert.NotNull(termParagraph.Descendants<Run>().First().RunProperties?.Bold);
        Assert.Contains(paragraphs, p => p.InnerText == "Pomaceous fruit");
        Assert.Contains(paragraphs, p => p.InnerText == "Comes in many colors");
    }

    [Fact]
    public void ConvertToFile_RendersTaskListCheckboxes()
    {
        const string markdown = "- [x] Done task\n- [ ] Not done\n";
        var path = Path.Combine(_tempDir, "chapter-task-list.docx");

        _converter.ConvertToFile(markdown, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var bodyText = document.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("☑", bodyText);
        Assert.Contains("☐", bodyText);
    }

    [Fact]
    public void ConvertToFile_RendersCustomContainerContent()
    {
        const string markdown = "::: {.smallcaps}\nStyled paragraph text.\n:::\n";
        var path = Path.Combine(_tempDir, "chapter-custom-container.docx");

        _converter.ConvertToFile(markdown, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        Assert.Contains("Styled paragraph text.", document.MainDocumentPart!.Document!.Body!.InnerText);
    }
}

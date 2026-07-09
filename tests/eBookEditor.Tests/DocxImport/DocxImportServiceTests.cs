using eBookEditor.DocxImport.Services;

namespace eBookEditor.Tests.DocxImport;

public class DocxImportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DocxImportService _importService = new();

    public DocxImportServiceTests()
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
    public void Import_SplitsIntoChaptersOnHeading1AndTextPattern()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(3, chapters.Count);
        Assert.Equal("Chapter One", chapters[0].Title);
        Assert.Equal("Chapter Two", chapters[1].Title);
        Assert.Equal("Chapter 3: The Finale", chapters[2].Title);
    }

    [Fact]
    public void Import_ConvertsBoldAndItalicRuns()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("**bold**", chapters[0].BodyMarkdown);
        Assert.Contains("*italic*", chapters[0].BodyMarkdown);
    }

    [Fact]
    public void Import_ConvertsHeading2ToMarkdownSubheading()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("## A Subsection", chapters[0].BodyMarkdown);
    }

    [Fact]
    public void Import_ConvertsNumberingListItemsToBulletMarkdown()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("- First bullet", chapters[0].BodyMarkdown);
        Assert.Contains("- Second bullet", chapters[0].BodyMarkdown);
    }

    [Fact]
    public void Import_KeepsPreambleContentAsIntroductionChapter()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithPreamble(Path.Combine(_tempDir, "preamble.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("Introduction", chapters[0].Title);
        Assert.Contains("Some preamble text", chapters[0].BodyMarkdown);
        Assert.Equal("Chapter One", chapters[1].Title);
    }

    [Fact]
    public void Import_ExtractsEmbeddedImageAndRewritesMarkdownReference()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "with-image.docx"), imageBytes);

        var chapters = _importService.Import(docxPath);

        Assert.True(chapters[0].BodyMarkdown.Contains("![](../images/image-1.jpg)"), chapters[0].BodyMarkdown);
        var image = Assert.Single(chapters[0].Images);
        Assert.Equal("image-1.jpg", image.FileName);
        Assert.Equal(imageBytes, image.Bytes);
    }

    [Fact]
    public void Import_ConvertsTableToMarkdownPipeTable()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithTable(Path.Combine(_tempDir, "with-table.docx"));

        var chapters = _importService.Import(docxPath);

        var body = chapters[0].BodyMarkdown;
        Assert.Contains("Before the table.", body);
        Assert.Contains("| Name | Role |", body);
        Assert.Contains("| --- | --- |", body);
        Assert.Contains("| Jane Doe | Author |", body);
        Assert.Contains("| Ed Itor | Editor |", body);
        Assert.Contains("After the table.", body);

        var tableIndex = body.IndexOf("| Name", StringComparison.Ordinal);
        var beforeIndex = body.IndexOf("Before the table.", StringComparison.Ordinal);
        var afterIndex = body.IndexOf("After the table.", StringComparison.Ordinal);
        Assert.True(beforeIndex < tableIndex);
        Assert.True(tableIndex < afterIndex);
    }

    [Fact]
    public void Import_PreservesHyperlinkTargetAsMarkdownLink()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithHyperlink(Path.Combine(_tempDir, "with-link.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("[our site](https://example.com/)", chapters[0].BodyMarkdown);
    }

    [Fact]
    public void Import_DoesNotSplitAHandTypedTableOfContentsListIntoChapters()
    {
        // Regression test for a real user request: a manually-typed "Table of Contents"
        // section listing chapter titles ("Chapter 1: Getting Ready", etc.) reads identically
        // to real chapter headings by text alone — without special handling, each entry became
        // its own bogus, empty chapter.
        var docxPath = DocxFixtureBuilder.BuildDocxWithHandTypedToc(Path.Combine(_tempDir, "hand-typed-toc.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("Chapter 1: Getting Ready", chapters[0].Title);
        Assert.Contains("Real content for chapter one.", chapters[0].BodyMarkdown);
        Assert.Equal("Chapter 2: Starting Out", chapters[1].Title);
        Assert.Contains("Real content for chapter two.", chapters[1].BodyMarkdown);
    }

    [Fact]
    public void Import_DoesNotSplitAWordFieldGeneratedTableOfContentsIntoChapters()
    {
        // Word's own Insert > Table of Contents field renders each entry in a "TOC1"-styled
        // paragraph — must be filtered out by style alone, regardless of what its text says.
        var docxPath = DocxFixtureBuilder.BuildDocxWithFieldGeneratedToc(Path.Combine(_tempDir, "field-toc.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("Chapter 1: Getting Ready", chapters[0].Title);
        Assert.Contains("Real content for chapter one.", chapters[0].BodyMarkdown);
        Assert.Equal("Chapter 2: Starting Out", chapters[1].Title);
        Assert.Contains("Real content for chapter two.", chapters[1].BodyMarkdown);
    }
}

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
}

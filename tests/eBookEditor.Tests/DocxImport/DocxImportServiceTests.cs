using eBookEditor.Core.Models;
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

        Assert.Contains("<strong>bold</strong>", chapters[0].Body);
        Assert.Contains("<em>italic</em>", chapters[0].Body);
    }

    [Fact]
    public void Import_ConvertsHeading2ToHtmlSubheading()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("<h2>A Subsection</h2>", chapters[0].Body);
    }

    [Fact]
    public void Import_WrapsConsecutiveListItemsInAUnorderedList()
    {
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "sample.docx"));

        var chapters = _importService.Import(docxPath);
        var body = chapters[0].Body;

        Assert.Contains("<li>First bullet</li>", body);
        Assert.Contains("<li>Second bullet</li>", body);

        var ulIndex = body.IndexOf("<ul>", StringComparison.Ordinal);
        var closeUlIndex = body.IndexOf("</ul>", StringComparison.Ordinal);
        var firstIndex = body.IndexOf("<li>First bullet</li>", StringComparison.Ordinal);
        var secondIndex = body.IndexOf("<li>Second bullet</li>", StringComparison.Ordinal);

        Assert.True(ulIndex >= 0 && ulIndex < firstIndex, "A <ul> should wrap the list items.");
        Assert.True(firstIndex < secondIndex && secondIndex < closeUlIndex, "Both bullets should be inside the same <ul>...</ul>.");
    }

    [Fact]
    public void Import_KeepsPreambleContentAsIntroductionChapter()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithPreamble(Path.Combine(_tempDir, "preamble.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(2, chapters.Count);
        Assert.Equal("Introduction", chapters[0].Title);
        Assert.Contains("Some preamble text", chapters[0].Body);
        Assert.Equal("Chapter One", chapters[1].Title);
    }

    [Fact]
    public void Import_ExtractsEmbeddedImageAndRewritesHtmlReference()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var docxPath = DocxFixtureBuilder.BuildSimpleDocx(Path.Combine(_tempDir, "with-image.docx"), imageBytes);

        var chapters = _importService.Import(docxPath);

        Assert.Contains("<img src=\"../images/image-1.jpg\" alt=\"\">", chapters[0].Body);
        var image = Assert.Single(chapters[0].Images);
        Assert.Equal("image-1.jpg", image.FileName);
        Assert.Equal(imageBytes, image.Bytes);
    }

    [Fact]
    public void Import_ConvertsTableToHtmlTable()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithTable(Path.Combine(_tempDir, "with-table.docx"));

        var chapters = _importService.Import(docxPath);

        var body = chapters[0].Body;
        Assert.Contains("Before the table.", body);
        Assert.Contains("<table>", body);
        Assert.Contains("<th>Name</th>", body);
        Assert.Contains("<th>Role</th>", body);
        Assert.Contains("<td>Jane Doe</td>", body);
        Assert.Contains("<td>Author</td>", body);
        Assert.Contains("<td>Ed Itor</td>", body);
        Assert.Contains("<td>Editor</td>", body);
        Assert.Contains("After the table.", body);

        var tableIndex = body.IndexOf("<table>", StringComparison.Ordinal);
        var beforeIndex = body.IndexOf("Before the table.", StringComparison.Ordinal);
        var afterIndex = body.IndexOf("After the table.", StringComparison.Ordinal);
        Assert.True(beforeIndex < tableIndex);
        Assert.True(tableIndex < afterIndex);
    }

    [Fact]
    public void Import_PreservesHyperlinkTargetAsHtmlLink()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithHyperlink(Path.Combine(_tempDir, "with-link.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Contains("<a href=\"https://example.com/\">our site</a>", chapters[0].Body);
    }

    [Fact]
    public void Import_InternalBookmarkHyperlink_BecomesASameDocumentFragmentLinkInsteadOfBeingDropped()
    {
        // Previously an Anchor-based (internal bookmark) hyperlink was dropped to plain text
        // entirely — DocxImportService alone (not ChapterImportService, which additionally runs
        // SameDocumentLinkConverter) should now at least preserve it as a raw same-document
        // fragment link, and the bookmark target paragraph should carry a matching raw id.
        var docxPath = DocxFixtureBuilder.BuildDocxWithInternalBookmarkLink(Path.Combine(_tempDir, "with-bookmark.docx"));

        var chapters = _importService.Import(docxPath);
        var body = chapters[0].Body;

        Assert.Contains("<a href=\"#_Ref_TheCaptain\">the captain&#39;s introduction</a>", body);
        Assert.Contains("id=\"_Ref_TheCaptain\"", body);
        // A hyperlink to a bookmark that's never actually defined is still preserved as a raw
        // fragment link (better than being silently dropped) even though it can't resolve.
        Assert.Contains("<a href=\"#_Ref_NeverDefined\">broken reference</a>", body);
    }

    [Fact]
    public void Import_SimpleFieldXeIndexEntry_SurvivesAsAnIndexEntryMarker()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithSimpleFieldIndexEntry(Path.Combine(_tempDir, "with-simple-xe.docx"));

        var chapters = _importService.Import(docxPath);
        var body = chapters[0].Body;

        Assert.Contains("class=\"index-entry\"", body);
        Assert.Contains("data-index-term=\"Captain\"", body);
        Assert.Contains("id=\"idx:captain-", body);
        Assert.Contains("Meet Captain Reyes.", body);
    }

    [Fact]
    public void Import_ComplexFieldXeIndexEntry_SurvivesAsAnIndexEntryMarker()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithComplexFieldIndexEntry(Path.Combine(_tempDir, "with-complex-xe.docx"));

        var chapters = _importService.Import(docxPath);
        var body = chapters[0].Body;

        Assert.Contains("class=\"index-entry\"", body);
        Assert.Contains("data-index-term=\"Captain\"", body);
        Assert.Contains("id=\"idx:captain-", body);
        Assert.Contains("Meet Captain Reyes.", body);
        // The field's own begin/instrText/end machinery must not leak into the visible body.
        Assert.DoesNotContain("XE", body);
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
        Assert.Contains("Real content for chapter one.", chapters[0].Body);
        Assert.Equal("Chapter 2: Starting Out", chapters[1].Title);
        Assert.Contains("Real content for chapter two.", chapters[1].Body);
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
        Assert.Contains("Real content for chapter one.", chapters[0].Body);
        Assert.Equal("Chapter 2: Starting Out", chapters[1].Title);
        Assert.Contains("Real content for chapter two.", chapters[1].Body);
    }

    [Fact]
    public void Import_ClassifiesRecognizedHeadingsAsFrontBackMatterAndDividers()
    {
        var docxPath = DocxFixtureBuilder.BuildDocxWithSpecialPages(Path.Combine(_tempDir, "special-pages.docx"));

        var chapters = _importService.Import(docxPath);

        Assert.Equal(4, chapters.Count);

        Assert.Equal("Preface", chapters[0].Title);
        Assert.Equal(SpineItemType.FrontMatter, chapters[0].Type);

        Assert.Equal("Part One", chapters[1].Title);
        Assert.Equal(SpineItemType.Chapter, chapters[1].Type);
        Assert.Equal(ChapterNumberMode.None, chapters[1].NumberMode);

        Assert.Equal("Chapter One", chapters[2].Title);
        Assert.Equal(SpineItemType.Chapter, chapters[2].Type);
        Assert.Equal(ChapterNumberMode.Auto, chapters[2].NumberMode);

        Assert.Equal("Afterword", chapters[3].Title);
        Assert.Equal(SpineItemType.BackMatter, chapters[3].Type);
    }
}

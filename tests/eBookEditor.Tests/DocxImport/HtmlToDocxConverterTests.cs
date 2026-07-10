using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Services;
using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.DocxImport;

public class HtmlToDocxConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HtmlToDocxConverter _converter = new();

    public HtmlToDocxConverterTests()
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
        const string html = """
            <p>Hello <strong>bold</strong> and <em>italic</em> text.</p>
            <h2>Subheading</h2>
            <ul><li>First item</li><li>Second item</li></ul>
            <ol><li>Step one</li><li>Step two</li></ol>
            """;
        var path = Path.Combine(_tempDir, "chapter.docx");

        _converter.ConvertToFile(html, "Chapter One", path);

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

        _converter.ConvertToFile("<p>Visit <a href=\"https://example.com\">our site</a> for more.</p>", "Chapter With Link", path);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        var hyperlink = mainPart.Document!.Body!.Descendants<Hyperlink>().Single();

        Assert.Equal("our site", hyperlink.InnerText);
        var relationship = mainPart.HyperlinkRelationships.Single(r => r.Id == hyperlink.Id);
        Assert.Equal("https://example.com/", relationship.Uri.ToString());
    }

    [Fact]
    public void ConvertToFile_LinkWithNoHref_RendersAsPlainText()
    {
        var path = Path.Combine(_tempDir, "chapter-empty-link.docx");

        _converter.ConvertToFile("<p><a>Not really a link</a>.</p>", "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;

        Assert.Empty(mainPart.HyperlinkRelationships);
        Assert.Contains("Not really a link", mainPart.Document!.Body!.InnerText);
    }

    [Fact]
    public void ConvertToFile_LinksToASpaceContainingPath_ProduceAWellFormedRelationship()
    {
        // Regression test for a real bug a user hit (originally against Markdown link syntax,
        // now against a real HTML href — the underlying AddHyperlinkRelationship risk is the
        // same either way): the TOC page links to chapter files whose relative path can
        // contain a space. Feeding that straight into WordprocessingDocument.
        // AddHyperlinkRelationship as an unescaped Uri writes a package the OpenXml SDK itself
        // flags as having a "malformed URI relationship" — in practice this showed up as a
        // literal "rewritten://<guid>" placeholder baked into the saved .docx's actual
        // relationship Target instead of the real path, which made Word/Pages refuse to open
        // the file ("isn't in the correct format"). The destination must be percent-encoded
        // before it ever reaches AddHyperlinkRelationship.
        var path = Path.Combine(_tempDir, "chapter-space-link.docx");

        _converter.ConvertToFile(
            "<ul><li><a href=\"chapters/001 - Getting Ready.md\">Chapter 1: Getting Ready</a></li></ul>",
            "Table of Contents", path);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        var relationship = mainPart.HyperlinkRelationships.Single();

        Assert.DoesNotContain("rewritten://", relationship.Uri.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", relationship.Uri.OriginalString, StringComparison.Ordinal);
        Assert.Equal("chapters/001%20-%20Getting%20Ready.md", relationship.Uri.OriginalString);

        var errors = new OpenXmlValidator().Validate(document).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => $"{e.Path?.XPath} :: {e.Description}")));
    }

    [Fact]
    public void ConvertToFile_RendersTables()
    {
        const string html = """
            <table>
            <tr><th>Name</th><th>Role</th></tr>
            <tr><td>Jane Doe</td><td>Author</td></tr>
            <tr><td>Ed Itor</td><td>Editor</td></tr>
            </table>
            """;
        var path = Path.Combine(_tempDir, "chapter-table.docx");

        _converter.ConvertToFile(html, "Chapter With Table", path);

        using var document = WordprocessingDocument.Open(path, false);
        var table = document.MainDocumentPart!.Document!.Body!.Descendants<Table>().Single();
        var rows = table.Elements<TableRow>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Name", rows[0].Elements<TableCell>().First().InnerText);
        Assert.Equal("Jane Doe", rows[1].Elements<TableCell>().First().InnerText);
        Assert.Equal("Editor", rows[2].Elements<TableCell>().ElementAt(1).InnerText);

        // Regression: a <w:tbl> without a <w:tblGrid> child (defining the column count) is not
        // valid OOXML — Word refuses to open the whole file with "isn't in the correct format",
        // not just skip the table. Same for <w:tblBorders> children out of schema order
        // (top, left, bottom, right, insideH, insideV).
        var grid = table.Elements<TableGrid>().SingleOrDefault();
        Assert.NotNull(grid);
        Assert.Equal(2, grid!.Elements<GridColumn>().Count());
    }

    [Fact]
    public void ConvertToFile_ProducesSchemaValidDocx_ForEveryFeatureCombined()
    {
        // Regression test for a real bug a user hit: opening an exported .docx in Word/Pages
        // failed with "isn't in the correct format" — caused by a missing <w:tblGrid> in every
        // table and <w:tblBorders> children in the wrong schema order, neither of which surface
        // as a .NET exception (DocumentFormat.OpenXml happily writes structurally-valid-looking
        // but schema-invalid XML unless you run it through OpenXmlValidator). This exercises
        // every block/inline kind in one document, including combined underline+highlight on a
        // single run (another real ordering bug this test caught: <w:highlight> must precede
        // <w:u> in <w:rPr>). Task lists have no HTML authoring convention yet (no toolbar
        // command produces one), so they're not part of this combined document. Footnotes get
        // their own dedicated tests below (real FootnotesPart output), not folded in here.
        const string html = """
            <h1>Chapter One</h1>
            <p>Plain text, <em>italic</em>, <strong>bold</strong>, <strong><em>bold italic</em></strong>.</p>
            <p><s>strike</s>, <mark>highlight</mark>, H<sub>2</sub>O, E=mc<sup>2</sup>, <u><mark>underlined and nested highlight</mark></u>.</p>
            <dl><dt>Term</dt><dd>Definition text here.</dd></dl>
            <div class="smallcaps"><p>Styled paragraph.</p></div>
            <table>
            <tr><th>A</th><th>B</th><th>C</th></tr>
            <tr><td>1</td><td>2</td><td>3</td></tr>
            <tr><td>4</td><td>5</td><td>6</td></tr>
            </table>
            <pre>code line</pre>
            <hr>
            <p>Text after a thematic break.</p>
            """;
        var path = Path.Combine(_tempDir, "chapter-full-validation.docx");

        _converter.ConvertToFile(html, "Test Book", path);

        using var document = WordprocessingDocument.Open(path, false);
        var errors = new OpenXmlValidator().Validate(document).ToList();

        Assert.True(errors.Count == 0,
            string.Join("\n", errors.Select(e => $"{e.Path?.XPath} :: {e.Description}")));
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

        _converter.ConvertToFile("<p><img src=\"../images/cover.png\" alt=\"Cover\"></p>", "Chapter With Image", path, chaptersDir);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        Assert.Single(mainPart.ImageParts);
        Assert.NotNull(mainPart.Document!.Body!.Descendants<Drawing>().SingleOrDefault());
    }

    [Fact]
    public void ConvertToFile_MissingImageIsSkippedWithoutError()
    {
        var path = Path.Combine(_tempDir, "chapter-missing-image.docx");

        _converter.ConvertToFile("<p><img src=\"../images/does-not-exist.png\" alt=\"Missing\"></p>", "Chapter", path, _tempDir);

        using var document = WordprocessingDocument.Open(path, false);
        Assert.Empty(document.MainDocumentPart!.ImageParts);
    }

    [Fact]
    public void ConvertToFile_RendersFencedCodeBlocksAsMonospaceParagraphs()
    {
        const string html = "<pre>def greet(name):\n    return f\"Hello, {name}!\"</pre>";
        var path = Path.Combine(_tempDir, "chapter-code.docx");

        _converter.ConvertToFile(html, "Chapter With Code", path);

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
        // HtmlBookAssembler.AssembleWholeBook separates sections with "<hr>"; the whole-book
        // Word export relies on this becoming a page break so each section still starts on
        // its own page, matching the EPUB/PDF exports' convention.
        const string html = "<p>First section.</p><hr><p>Second section.</p>";
        var path = Path.Combine(_tempDir, "chapter-thematic-break.docx");

        _converter.ConvertToFile(html, "Chapter With Break", path);

        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var pageBreak = body.Descendants<Break>().SingleOrDefault(b => b.Type?.Value == BreakValues.Page);

        Assert.NotNull(pageBreak);
    }

    [Fact]
    public void ConvertToFile_RendersTagBasedEmphasisCorrectly_NotAsBoldOrItalic()
    {
        // Regression coverage for HtmlStyleDocument's user-agent baseline stylesheet (see its
        // own doc comment): strikethrough/highlight/subscript/superscript/underline tags must
        // resolve to their own real formatting, not fall through to bold/italic or nothing.
        const string html = "<p><s>struck</s> <mark>marked</mark> H<sub>2</sub>O E=mc<sup>2</sup> <u>inserted</u></p>";
        var path = Path.Combine(_tempDir, "chapter-tag-emphasis.docx");

        _converter.ConvertToFile(html, "Chapter", path);

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
        const string html = "<dl><dt>Apple</dt><dd>Pomaceous fruit</dd><dd>Comes in many colors</dd></dl>";
        var path = Path.Combine(_tempDir, "chapter-definition-list.docx");

        _converter.ConvertToFile(html, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

        var termParagraph = paragraphs.Single(p => p.InnerText == "Apple");
        Assert.NotNull(termParagraph.Descendants<Run>().First().RunProperties?.Bold);
        Assert.Contains(paragraphs, p => p.InnerText == "Pomaceous fruit");
        Assert.Contains(paragraphs, p => p.InnerText == "Comes in many colors");
    }

    [Fact]
    public void ConvertToFile_RendersInsertImageContainerShape_ImageAndCaptionBothPresent()
    {
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var imagesDir = Path.Combine(_tempDir, "images");
        Directory.CreateDirectory(imagesDir);
        File.WriteAllBytes(Path.Combine(imagesDir, "photo.jpg"), pngBytes);
        var chaptersDir = Path.Combine(_tempDir, "chapters");
        Directory.CreateDirectory(chaptersDir);

        const string html = """
            <figure>
            <img src="../images/photo.jpg" alt="A photo">
            <figcaption class="caption">Caption text</figcaption>
            </figure>
            """;
        var path = Path.Combine(_tempDir, "chapter-insert-image.docx");

        _converter.ConvertToFile(html, "Chapter", path, chaptersDir);

        using var document = WordprocessingDocument.Open(path, false);
        var mainPart = document.MainDocumentPart!;
        Assert.Single(mainPart.ImageParts);
        Assert.Contains("Caption text", mainPart.Document!.Body!.InnerText);
    }

    [Fact]
    public void ConvertToFile_AppliesEditorStyleCatalogClassesViaRealCss()
    {
        // The core claim of Phase 5's AngleSharp.Css integration: EditorStyleCatalog classes
        // (see DefaultStylesheet.cs) actually affect the rendered .docx now, using real Word
        // primitives the predecessor converter never emitted for these classes at all —
        // smallCaps and caps are exact matches (Word has real primitives for both), unlike
        // QuestPDF/PDF, which has neither and must skip or approximate them instead.
        const string html = """
            <p class="smallcaps">Small caps text.</p>
            <p class="all-caps">shout this</p>
            <div class="attribution"><p>An Author</p></div>
            """;
        var path = Path.Combine(_tempDir, "chapter-styled.docx");

        _converter.ConvertToFile(html, "Chapter", path, templateCss: DefaultStylesheet.Css);

        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

        var smallCapsRun = paragraphs.Single(p => p.InnerText.Contains("Small caps")).Descendants<Run>().First();
        Assert.NotNull(smallCapsRun.RunProperties?.SmallCaps);

        var allCapsRun = paragraphs.Single(p => p.InnerText.Contains("shout this")).Descendants<Run>().First();
        Assert.NotNull(allCapsRun.RunProperties?.Caps);

        var attributionParagraph = paragraphs.Single(p => p.InnerText == "An Author");
        Assert.Equal(JustificationValues.Right, attributionParagraph.ParagraphProperties!.Justification!.Val!.Value);
    }

    private const string FootnoteHtml = """
        <p>Some text with a note.<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup></p>
        <div class="footnotes">
        <hr>
        <ol>
        <li id="fn:1"><p>The note text. <a href="#fnref:1" class="footnote-back-ref">&#8617;</a></p></li>
        </ol>
        </div>
        """;

    [Fact]
    public void ConvertToFile_FootnoteReference_BecomesARealFootnoteReferenceRun()
    {
        var path = Path.Combine(_tempDir, "chapter-footnote.docx");

        _converter.ConvertToFile(FootnoteHtml, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document!.Body!;

        // The reference lives in the body as a real w:footnoteReference, not literal "1" text —
        // and the "<div class='footnotes'>" block that held the note's own text is gone from
        // the body entirely (its content moved into the FootnotesPart instead).
        var reference = body.Descendants<FootnoteReference>().Single();
        Assert.DoesNotContain(body.Descendants<Text>(), t => t.Text == "1");
        Assert.DoesNotContain("footnotes", body.InnerText);

        var footnotesPart = document.MainDocumentPart!.FootnotesPart;
        Assert.NotNull(footnotesPart);
        var noteFootnote = footnotesPart!.Footnotes!.Elements<Footnote>().Single(f => f.Id!.Value == reference.Id!.Value);
        Assert.Contains("The note text.", noteFootnote.InnerText);
        // The back-reference arrow is Word's own built-in footnote-pane behavior — this app's
        // own "↩" text shouldn't also appear inside the real footnote content.
        Assert.DoesNotContain('↩', noteFootnote.InnerText);
    }

    [Fact]
    public void ConvertToFile_FootnotesPart_IncludesRequiredSeparatorEntries()
    {
        var path = Path.Combine(_tempDir, "chapter-footnote-separators.docx");

        _converter.ConvertToFile(FootnoteHtml, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var footnotes = document.MainDocumentPart!.FootnotesPart!.Footnotes!.Elements<Footnote>().ToList();

        Assert.Contains(footnotes, f => f.Type?.Value == FootnoteEndnoteValues.Separator);
        Assert.Contains(footnotes, f => f.Type?.Value == FootnoteEndnoteValues.ContinuationSeparator);
    }

    [Fact]
    public void ConvertToFile_ProducesSchemaValidDocx_WithAFootnote()
    {
        var path = Path.Combine(_tempDir, "chapter-footnote-valid.docx");

        _converter.ConvertToFile(FootnoteHtml, "Chapter", path);

        using var document = WordprocessingDocument.Open(path, false);
        var errors = new OpenXmlValidator().Validate(document).ToList();

        Assert.True(errors.Count == 0,
            string.Join("\n", errors.Select(e => $"{e.Path?.XPath} :: {e.Description}")));
    }

    [Fact]
    public void ConvertToFile_TwoChaptersEachRestartingAtFootnoteOne_GetGloballyUniqueWordIds()
    {
        // HtmlBookAssembler concatenates chapters into one HTML string for whole-book export,
        // and this app's own footnote numbering restarts at 1 in every chapter (see
        // MainWindow.OnInsertFootnoteClick) — two "id='fn:1'" li elements from different
        // chapters would collide in a single FootnotesPart unless each gets its own Word id.
        const string html = """
            <h1>Chapter One</h1>
            <p>First chapter note.<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup></p>
            <div class="footnotes"><ol><li id="fn:1"><p>Note from chapter one.</p></li></ol></div>
            <hr>
            <h1>Chapter Two</h1>
            <p>Second chapter note.<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup></p>
            <div class="footnotes"><ol><li id="fn:1"><p>Note from chapter two.</p></li></ol></div>
            """;
        var path = Path.Combine(_tempDir, "whole-book-footnotes.docx");

        _converter.ConvertToFile(html, "Test Book", path);

        using var document = WordprocessingDocument.Open(path, false);
        var references = document.MainDocumentPart!.Document!.Body!.Descendants<FootnoteReference>().ToList();
        Assert.Equal(2, references.Count);
        Assert.NotEqual(references[0].Id!.Value, references[1].Id!.Value);

        var realFootnotes = document.MainDocumentPart!.FootnotesPart!.Footnotes!.Elements<Footnote>()
            .Where(f => f.Type is null)
            .ToList();
        Assert.Equal(2, realFootnotes.Count);
        Assert.Contains(realFootnotes, f => f.InnerText.Contains("Note from chapter one."));
        Assert.Contains(realFootnotes, f => f.InnerText.Contains("Note from chapter two."));

        var errors = new OpenXmlValidator().Validate(document).ToList();
        Assert.True(errors.Count == 0,
            string.Join("\n", errors.Select(e => $"{e.Path?.XPath} :: {e.Description}")));
    }
}

using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Html.Services;
using eBookEditor.Pdf.Services;
using UglyToad.PdfPig;

namespace eBookEditor.Tests.Pdf;

public class PdfBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly PdfBuilder _pdfBuilder = new();

    public PdfBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EbookProject BuildSampleProject()
    {
        var metadata = new BookMetadata
        {
            Title = "Pdf Test Book",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            CopyrightYear = 2026,
            Isbn13 = "9780306406157",
            Language = "en"
        };
        var project = _projectService.CreateProject(_tempDir, "Pdf Test Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter One");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter One" },
            "<p>Hello <strong>world</strong>, this is the first chapter. It has a few words in it.</p>");
        _spineService.AddChapter(project, "Chapter One", relativePath);

        _pageGenerator.RegenerateAllGeneratedPages(project);
        _projectService.SaveProject(project);
        return project;
    }

    [Fact]
    public void Build_ProducesAValidPdfFile()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.True(File.Exists(outputPath));
        var header = new byte[5];
        using (var stream = File.OpenRead(outputPath))
            stream.ReadExactly(header);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
        Assert.Equal(outputPath, result.OutputPath);
    }

    [Fact]
    public void Build_RendersTheChaptersTitleAsAHeading()
    {
        // Chapter files store the title only in front matter, never in the body (see
        // ChapterHeadingHtml) — BuildSampleProject's chapter body has no heading text at all,
        // so this only passes if PdfBuilder actually synthesizes one from the spine item.
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var allText = string.Join(" ", Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text));

        Assert.Contains("Chapter 1: Chapter One", allText);
    }

    [Fact]
    public void Build_ReturnsAtLeastOnePagePerSpineItem()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.True(result.PageCount >= project.Spine.Count,
            $"Expected at least {project.Spine.Count} pages (one per spine item), got {result.PageCount}.");
    }

    [Fact]
    public void Build_CountsWordsFromChaptersOnly()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        // "Hello world, this is the first chapter. It has a few words in it." = 14 words —
        // counted from the parsed HTML's text content (HtmlText.CountWords), not the raw
        // stored markup, so the "<p>"/"<strong>" tags themselves don't get miscounted as
        // extra/glued-together "words".
        Assert.Equal(14, result.WordCount);
    }

    [Fact]
    public void Build_RespectsSelectedPageSize()
    {
        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with { PdfPageSize = "US Letter" };
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        _pdfBuilder.Build(project, outputPath);

        var pdfBytes = File.ReadAllBytes(outputPath);
        var pdfText = System.Text.Encoding.Latin1.GetString(pdfBytes);
        // US Letter is 612x792 points; QuestPDF writes /MediaBox [0 0 612 792] per page.
        Assert.Contains("612", pdfText);
        Assert.Contains("792", pdfText);
    }

    [Fact]
    public void Build_RendersChapterWithTableImageAndFootnote()
    {
        var project = BuildSampleProject();

        // Minimal valid 1x1 PNG.
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        Directory.CreateDirectory(project.ImagesDir);
        File.WriteAllBytes(Path.Combine(project.ImagesDir, "diagram.png"), pngBytes);

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            """
            <p>Some text with a footnote.</p>
            <table>
            <tr><th>Name</th><th>Role</th></tr>
            <tr><td>Jane Doe</td><td>Author</td></tr>
            </table>
            <p><img src="../images/diagram.png" alt="Diagram"></p>
            """);
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.True(File.Exists(outputPath));
        var header = new byte[5];
        using (var stream = File.OpenRead(outputPath))
            stream.ReadExactly(header);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
        Assert.True(result.PageCount >= project.Spine.Count);
    }

    [Fact]
    public void Build_RendersARealFootnoteReferenceAndNotesSection()
    {
        var project = BuildSampleProject();

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            """
            <p>Some text with a note.<sup id="fnref:1"><a href="#fn:1" class="footnote-ref">1</a></sup></p>
            <div class="footnotes">
            <hr>
            <ol>
            <li id="fn:1"><p>Gruber is also known for the blog Daring Fireball. <a href="#fnref:1" class="footnote-back-ref">&#8617;</a></p></li>
            </ol>
            </div>
            """);
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var allText = string.Join(" ", Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text));

        Assert.Contains("Notes", allText);
        Assert.Contains("Gruber is also known for the blog Daring Fireball.", allText);
        // The back-reference arrow has no in-document jump target in PDF (see
        // HtmlToPdfRenderer.RenderFootnotes) and shouldn't appear as dead link text.
        Assert.DoesNotContain('↩', allText);
    }

    [Fact]
    public void Build_WithVellumSerifTemplate_EmbedsTheTemplatesFonts()
    {
        var repoRoot = FindRepoRoot();
        var fontsDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "fonts");
        var templatesDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "templates");
        var pdfBuilder = new PdfBuilder(new TemplateService(templatesDir), new FontService(fontsDir));

        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with { SelectedTemplate = "Vellum Serif" };
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        pdfBuilder.Build(project, outputPath);

        var pdfBytes = File.ReadAllBytes(outputPath);
        var pdfText = System.Text.Encoding.Latin1.GetString(pdfBytes);
        Assert.Contains("Alegreya", pdfText);
    }

    [Fact]
    public void Build_RendersChapterWithFencedCodeBlock()
    {
        var project = BuildSampleProject();

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            """
            <p>Some text before a code sample.</p>
            <pre>def greet(name):
                return f"Hello, {name}!"</pre>
            """);
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.True(File.Exists(outputPath));
        var header = new byte[5];
        using (var stream = File.OpenRead(outputPath))
            stream.ReadExactly(header);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
        Assert.True(result.PageCount >= project.Spine.Count);
    }

    [Fact]
    public void Build_ImprintPageStaysOnOnePhysicalPage()
    {
        // Front matter (title, imprint, toc) + 1 chapter + back matter (about-author) = 5
        // spine items; each should render as exactly one physical page for this short
        // sample content. Regression test: the imprint page used to overflow onto a second
        // physical page (its ExtendVertical-pinned copyright block was pushed past the page
        // boundary instead of pinning to this page's bottom), inflating PageCount to 6.
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.Equal(project.Spine.Count, result.PageCount);
    }

    [Fact]
    public void Build_ImprintPageWithLongDisclaimer_DoesNotThrow()
    {
        // Regression test for QuestPDF.Drawing.Exceptions.DocumentLayoutException: an earlier
        // fix attempt bounded the imprint page to a hard-computed page height, which threw
        // when real content (a long disclaimer, like this project's actual default one)
        // needed even slightly more room than that estimate.
        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with
        {
            CopyrightDisclaimer = string.Join(" ", Enumerable.Repeat(BookMetadata.DefaultDisclaimerText, 3))
        };
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        var result = _pdfBuilder.Build(project, outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(result.PageCount >= project.Spine.Count);
    }

    [Fact]
    public void Build_TableOfContentsPageNumbers_MatchTheHeaderFooterFormatting()
    {
        // Regression test: the TOC previously showed a garbage page number for the imprint
        // page (BeginPageNumberOfSection referenced a Section that was never actually
        // defined, since only chapters were wrapped in .Section(...)), and even once that was
        // fixed, showed a raw arabic page number that didn't match the roman numeral the
        // header/footer actually print on that physical page.
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var tocPageText = document.GetPage(3).Text; // 1 title, 2 imprint, 3 toc

        Assert.Contains("Imprint", tocPageText);
        Assert.Contains("Chapter 1: Chapter One", tocPageText);
        Assert.DoesNotContain("123", tocPageText);
        // The imprint entry's page number must be the roman numeral the imprint page itself
        // is labeled with (ii), not a raw/garbage arabic number.
        var imprintIndex = tocPageText.IndexOf("Imprint", StringComparison.Ordinal);
        var nextEntryIndex = tocPageText.IndexOf("Chapter 1", StringComparison.Ordinal);
        var imprintEntry = tocPageText[imprintIndex..nextEntryIndex];
        Assert.Contains("ii", imprintEntry);
        Assert.DoesNotContain("iii", imprintEntry);
    }

    [Fact]
    public void Build_HeaderAndFooter_FollowLeftRightPageConvention()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);

        // Page 2 (imprint) is even/left: header = page# + book title, footer = page# + author.
        var leftPageText = document.GetPage(2).Text;
        Assert.Contains("ii", leftPageText);
        Assert.Contains("Pdf Test Book", leftPageText);
        Assert.Contains("Jane Author", leftPageText);

        // Page 4 (the chapter) is even/left too (title=1, imprint=2, toc=3, chapter=4).
        var chapterPageText = document.GetPage(4).Text;
        Assert.Contains("Pdf Test Book", chapterPageText);
        Assert.Contains("Jane Author", chapterPageText);

        // Page 3 (toc) is odd/right: header should name the current chapter — none has
        // started yet, so just the page number; footer shows only the page number too.
        var tocPageText = document.GetPage(3).Text;
        Assert.DoesNotContain("Pdf Test Book", tocPageText);
        Assert.DoesNotContain("Jane Author", tocPageText);
    }

    [Fact]
    public void Build_RendersExtendedFormattingWithoutError_DefinitionListsAndStyledDivs()
    {
        var project = BuildSampleProject();

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            """
            <p>Some <s>struck</s> and <mark>marked</mark> and H<sub>2</sub>O and E=mc<sup>2</sup> text.</p>
            <dl><dt>Apple</dt><dd>Pomaceous fruit</dd></dl>
            <div class="smallcaps"><p>A styled paragraph.</p></div>
            """);
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");

        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var pageTexts = Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text);
        var allText = string.Join(" ", pageTexts);

        Assert.Contains("struck", allText);
        Assert.Contains("marked", allText);
        Assert.Contains("Apple", allText);
        Assert.Contains("Pomaceousfruit", allText.Replace(" ", ""));
        Assert.Contains("styledparagraph", allText.Replace(" ", ""));
    }

    [Fact]
    public void Build_RendersInsertImageContainerShape_WithoutErrorAndCaptionTextPresent()
    {
        // MainWindow.OnInsertImageClick's planned HTML shape (a <figure>/<figcaption> pair —
        // see PageGeneratorService's design notes). ".caption" now has a real visual effect
        // via AngleSharp.Css (a smaller font size, see DefaultStylesheet.cs), unlike the
        // pre-Phase-5 renderer, which had no CSS engine at all — this just checks it renders
        // without throwing and the caption text survives the structure.
        var project = BuildSampleProject();
        var imagesDir = Path.Combine(project.DirectoryPath, "images");
        Directory.CreateDirectory(imagesDir);
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        File.WriteAllBytes(Path.Combine(imagesDir, "photo.jpg"), pngBytes);

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            """
            <figure>
            <img src="../images/photo.jpg" alt="A photo">
            <figcaption class="caption">Caption text</figcaption>
            </figure>
            """);
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var allText = string.Join(" ", Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text));

        Assert.Contains("Captiontext", allText.Replace(" ", ""));
    }

    [Fact]
    public void Build_AppliesEditorStyleCatalogClassesViaRealCss()
    {
        // Regression coverage for the actual Phase 5 feature: a class the template CSS
        // defines (here, the shipped default template's own EditorStyleCatalog rules) must
        // visibly affect rendering, not just survive structurally. There's no easy way to
        // assert visual formatting from extracted PDF text, so this checks the one CSS effect
        // that also changes extracted text content: text-transform: uppercase on .all-caps.
        var project = BuildSampleProject();

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter Two" },
            "<p class=\"all-caps\">shout this line</p>");
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var allText = string.Join(" ", Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text));

        Assert.Contains("SHOUT THIS LINE", allText);
    }

    [Fact]
    public void Build_TocPageNumberAndRunningHeaderResolveCorrectly_ForAnEmptyChapter()
    {
        // Regression test for a real bug a user hit: an unwritten "New Chapter" stub (created
        // via ChapterFileService.CreateNewChapterFile, which leaves the body empty until the
        // author writes something) has zero HTML nodes, so RenderHtmlBody's loop never ran at
        // all — meaning the chapter's QuestPDF .Section() never got registered anywhere. That
        // broke two things: the TOC's page-number lookup for that chapter had nothing to
        // resolve and rendered a literal "?", and the running header's "current chapter"
        // lookup stayed stuck on whichever earlier chapter DID register, for the rest of the
        // book.
        var project = BuildSampleProject();
        var emptyChapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter Two");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, emptyChapterPath).Replace('\\', '/');
        _spineService.AddChapter(project, "Chapter Two", relativePath);
        _chapterFileService.SyncChapterFileNames(project);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.pdf");
        _pdfBuilder.Build(project, outputPath);

        using var document = PdfDocument.Open(outputPath);
        var pageTexts = Enumerable.Range(1, document.NumberOfPages).Select(i => document.GetPage(i).Text).ToList();
        var tocPageText = pageTexts.Single(t => t.Contains("Table of Contents", StringComparison.Ordinal));
        var emptyChapterPageText = pageTexts.Single(t => t.Contains("Chapter Two", StringComparison.Ordinal) && !t.Contains("Table of Contents", StringComparison.Ordinal));

        Assert.DoesNotContain("?", tocPageText);
        Assert.Contains("Chapter Two", emptyChapterPageText);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eBookEditor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test assembly location.");
    }
}

using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Markdown.Services;
using eBookEditor.Pdf.Services;

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
            "Hello **world**, this is the first chapter. It has a few words in it.");
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

        // "Hello world, this is the first chapter. It has a few words in it." = 14 words.
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
            Some text with a footnote[^1].

            | Name | Role |
            | --- | --- |
            | Jane Doe | Author |

            ![Diagram](../images/diagram.png)

            [^1]: This is the note.
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
            Some text before a code sample.

            ```
            def greet(name):
                return f"Hello, {name}!"
            ```
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

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eBookEditor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test assembly location.");
    }
}

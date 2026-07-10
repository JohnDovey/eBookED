using eBookEditor.ChapterImport.Services;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Tests.ChapterImport;

public class OrphanChapterScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly OrphanChapterScanner _scanner = new();

    public OrphanChapterScannerTests()
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
    public void FindOrphanedChapterFiles_ReturnsFilesNotReferencedBySpine()
    {
        var project = _projectService.CreateProject(_tempDir, "Orphan Book", new BookMetadata { Title = "Orphan Book" });
        var trackedPath = Path.Combine(project.ChaptersDir, "tracked.ebhtml");
        File.WriteAllText(trackedPath, "Tracked content.");
        _spineService.AddChapter(project, "Tracked", "chapters/tracked.ebhtml");

        var orphanPath = Path.Combine(project.ChaptersDir, "23. Orphan Chapter.ebhtml");
        File.WriteAllText(orphanPath, "Orphaned content.");

        var orphans = _scanner.FindOrphanedChapterFiles(project);

        var orphan = Assert.Single(orphans);
        Assert.Equal(orphanPath, orphan);
    }

    [Fact]
    public void FindOrphanedChapterFiles_ReturnsEmptyWhenEverythingIsTracked()
    {
        var project = _projectService.CreateProject(_tempDir, "Complete Book", new BookMetadata { Title = "Complete Book" });
        File.WriteAllText(Path.Combine(project.ChaptersDir, "tracked.ebhtml"), "Tracked content.");
        _spineService.AddChapter(project, "Tracked", "chapters/tracked.ebhtml");

        Assert.Empty(_scanner.FindOrphanedChapterFiles(project));
    }

    [Fact]
    public void FindOrphanedChapterFiles_AlsoFindsLegacyMarkdownDocxAndHtmlFiles()
    {
        var project = _projectService.CreateProject(_tempDir, "Mixed Format Book", new BookMetadata { Title = "Mixed Format Book" });

        var markdownPath = Path.Combine(project.ChaptersDir, "1. Legacy.md");
        File.WriteAllText(markdownPath, "Legacy content.");
        var docxPath = Path.Combine(project.ChaptersDir, "2. Word Doc.docx");
        File.WriteAllText(docxPath, "not a real docx, just needs to exist for the scan");
        var htmlPath = Path.Combine(project.ChaptersDir, "3. Web Page.html");
        File.WriteAllText(htmlPath, "<p>Web content.</p>");
        var htmPath = Path.Combine(project.ChaptersDir, "4. Web Page Two.htm");
        File.WriteAllText(htmPath, "<p>More web content.</p>");

        var orphans = _scanner.FindOrphanedChapterFiles(project);

        Assert.Equal(
            new[] { markdownPath, docxPath, htmlPath, htmPath }.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
            orphans);
    }
}

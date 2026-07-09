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
        var trackedPath = Path.Combine(project.ChaptersDir, "tracked.md");
        File.WriteAllText(trackedPath, "Tracked content.");
        _spineService.AddChapter(project, "Tracked", "chapters/tracked.md");

        var orphanPath = Path.Combine(project.ChaptersDir, "23. Orphan Chapter.md");
        File.WriteAllText(orphanPath, "Orphaned content.");

        var orphans = _scanner.FindOrphanedChapterFiles(project);

        var orphan = Assert.Single(orphans);
        Assert.Equal(orphanPath, orphan);
    }

    [Fact]
    public void FindOrphanedChapterFiles_ReturnsEmptyWhenEverythingIsTracked()
    {
        var project = _projectService.CreateProject(_tempDir, "Complete Book", new BookMetadata { Title = "Complete Book" });
        File.WriteAllText(Path.Combine(project.ChaptersDir, "tracked.md"), "Tracked content.");
        _spineService.AddChapter(project, "Tracked", "chapters/tracked.md");

        Assert.Empty(_scanner.FindOrphanedChapterFiles(project));
    }
}

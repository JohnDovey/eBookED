using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class ChapterFileServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ChapterFileService _service = new();

    public ChapterFileServiceTests()
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
    public void WriteThenReadChapter_RoundTripsFrontMatterAndBody()
    {
        var path = Path.Combine(_tempDir, "chapter.ebhtml");
        var frontMatter = new ChapterFrontMatter
        {
            Title = "The Beginning",
            Subtitle = "In which our hero appears",
            NumberMode = ChapterNumberMode.Override,
            NumberOverride = 5
        };

        _service.WriteChapter(path, frontMatter, "Once upon a time...");
        var (readFrontMatter, body) = _service.ReadChapter(path);

        Assert.Equal("The Beginning", readFrontMatter.Title);
        Assert.Equal("In which our hero appears", readFrontMatter.Subtitle);
        Assert.Equal(ChapterNumberMode.Override, readFrontMatter.NumberMode);
        Assert.Equal(5, readFrontMatter.NumberOverride);
        Assert.Equal("Once upon a time...", body.Trim());
    }

    [Fact]
    public void ParseChapter_HandlesTextWithoutFrontMatter()
    {
        var (frontMatter, body) = _service.ParseChapter("Just plain markdown, no front matter.");

        Assert.Null(frontMatter.Title);
        Assert.Equal("Just plain markdown, no front matter.", body);
    }

    [Fact]
    public void ReplaceBody_PreservesFrontMatterVerbatimAndSwapsOnlyBody()
    {
        var original = "---\ntitle: Chapter One\nnumberMode: Auto\n---\n\n<p>Old body.</p>";

        var replaced = _service.ReplaceBody(original, "<p>New body.</p>");

        var (frontMatter, body) = _service.ParseChapter(replaced);
        Assert.Equal("Chapter One", frontMatter.Title);
        Assert.Equal("<p>New body.</p>", body);
    }

    [Fact]
    public void ReplaceBody_NoFrontMatter_ReturnsBodyAsIs()
    {
        var replaced = _service.ReplaceBody("<p>No front matter here.</p>", "<p>New body.</p>");

        Assert.Equal("<p>New body.</p>", replaced);
    }

    [Fact]
    public void CreateNewChapterFile_GeneratesSlugifiedUniqueFileName()
    {
        var chaptersDir = Path.Combine(_tempDir, "chapters");
        Directory.CreateDirectory(chaptersDir);

        var pathA = _service.CreateNewChapterFile(chaptersDir, "The Arrival!");
        var pathB = _service.CreateNewChapterFile(chaptersDir, "The Arrival!");

        Assert.True(File.Exists(pathA));
        Assert.True(File.Exists(pathB));
        Assert.NotEqual(pathA, pathB);
        Assert.StartsWith("the-arrival-", Path.GetFileName(pathA));
    }

    [Fact]
    public void SyncChapterFileNames_RenamesFileToMatchResolvedNumberAndUpdatesRelativePath()
    {
        var projectService = new ProjectService();
        var spineService = new SpineService();
        var project = projectService.CreateProject(_tempDir, "Sync Test Book", new BookMetadata { Title = "Sync Test Book" });

        var path = _service.CreateNewChapterFile(project.ChaptersDir, "First Chapter");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, path).Replace('\\', '/');
        var item = spineService.AddChapter(project, "First Chapter", relativePath);

        _service.SyncChapterFileNames(project);

        var updated = project.Spine.Single(i => i.Id == item.Id);
        Assert.Equal("chapters/001-First-Chapter.ebhtml", updated.RelativePath);
        Assert.True(File.Exists(project.ResolvePath(updated)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SyncChapterFileNames_HandlesSwappedPositionsWithoutCollision()
    {
        var projectService = new ProjectService();
        var spineService = new SpineService();
        var project = projectService.CreateProject(_tempDir, "Swap Test Book", new BookMetadata { Title = "Swap Test Book" });

        var pathA = _service.CreateNewChapterFile(project.ChaptersDir, "Alpha");
        var itemA = spineService.AddChapter(project, "Alpha", Path.GetRelativePath(project.DirectoryPath, pathA).Replace('\\', '/'));
        var pathB = _service.CreateNewChapterFile(project.ChaptersDir, "Beta");
        var itemB = spineService.AddChapter(project, "Beta", Path.GetRelativePath(project.DirectoryPath, pathB).Replace('\\', '/'));
        _service.SyncChapterFileNames(project);

        // Swap: Beta becomes chapter 1, Alpha becomes chapter 2 — their desired file names
        // trade places, which must not collide or lose data mid-rename.
        spineService.ReorderChapters(project, [itemB.Id, itemA.Id]);
        _service.SyncChapterFileNames(project);

        var updatedA = project.Spine.Single(i => i.Id == itemA.Id);
        var updatedB = project.Spine.Single(i => i.Id == itemB.Id);
        Assert.Equal("chapters/002-Alpha.ebhtml", updatedA.RelativePath);
        Assert.Equal("chapters/001-Beta.ebhtml", updatedB.RelativePath);
        Assert.True(File.Exists(project.ResolvePath(updatedA)));
        Assert.True(File.Exists(project.ResolvePath(updatedB)));
    }
}

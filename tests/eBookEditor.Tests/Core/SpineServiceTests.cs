using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class SpineServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();

    public SpineServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EbookProject NewProject(string name = "Spine Book")
    {
        var project = _projectService.CreateProject(_tempDir, name, new BookMetadata { Title = name });
        File.WriteAllText(Path.Combine(project.ChaptersDir, "one.md"), "One");
        File.WriteAllText(Path.Combine(project.ChaptersDir, "two.md"), "Two");
        File.WriteAllText(Path.Combine(project.ChaptersDir, "three.md"), "Three");
        return project;
    }

    [Fact]
    public void AddChapter_PlacesChapterBeforeBackMatterAndAfterFrontMatter()
    {
        var project = NewProject();

        var chapter = _spineService.AddChapter(project, "Chapter One", "chapters/one.md");

        var ordered = project.Spine.OrderBy(i => i.Order).ToList();
        Assert.Equal(SpineItemType.FrontMatter, ordered[0].Type);
        Assert.Equal(SpineItemType.FrontMatter, ordered[1].Type);
        Assert.Equal(SpineItemType.FrontMatter, ordered[2].Type);
        Assert.Equal(chapter.Id, ordered[3].Id);
        Assert.Equal(SpineItemType.BackMatter, ordered[4].Type);
    }

    [Fact]
    public void RenumberChapters_AssignsSequentialAutoNumbers()
    {
        var project = NewProject();
        _spineService.AddChapter(project, "One", "chapters/one.md");
        _spineService.AddChapter(project, "Two", "chapters/two.md");
        var three = _spineService.AddChapter(project, "Three", "chapters/three.md");

        _spineService.RenumberChapters(project);

        var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal([1, 2, 3], chapters.Select(c => c.ResolvedNumber));
        Assert.Equal(3, three.ResolvedNumber);
    }

    [Fact]
    public void RenumberChapters_RespectsOverrideAndNoneModes()
    {
        var project = NewProject();
        _spineService.AddChapter(project, "One", "chapters/one.md");
        var overridden = _spineService.AddChapter(project, "Two", "chapters/two.md");
        overridden.NumberMode = ChapterNumberMode.Override;
        overridden.NumberOverride = 99;
        var unnumbered = _spineService.AddChapter(project, "Three", "chapters/three.md");
        unnumbered.NumberMode = ChapterNumberMode.None;

        _spineService.RenumberChapters(project);

        Assert.Equal(1, project.Spine.Single(i => i.Title == "One").ResolvedNumber);
        Assert.Equal(99, overridden.ResolvedNumber);
        Assert.Null(unnumbered.ResolvedNumber);
    }

    [Fact]
    public void ReorderChapters_AppliesNewOrderAndRenumbers()
    {
        var project = NewProject();
        var one = _spineService.AddChapter(project, "One", "chapters/one.md");
        var two = _spineService.AddChapter(project, "Two", "chapters/two.md");
        var three = _spineService.AddChapter(project, "Three", "chapters/three.md");

        _spineService.ReorderChapters(project, [three.Id, one.Id, two.Id]);

        var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["Three", "One", "Two"], chapters.Select(c => c.Title));
        Assert.Equal([1, 2, 3], chapters.Select(c => c.ResolvedNumber));
    }

    [Fact]
    public void ReorderChapters_ThrowsWhenIdSetDoesNotMatch()
    {
        var project = NewProject();
        _spineService.AddChapter(project, "One", "chapters/one.md");

        Assert.Throws<ArgumentException>(() => _spineService.ReorderChapters(project, [Guid.NewGuid()]));
    }

    [Fact]
    public void AddChapter_WithPositionHint_InsertsAtThatChapterPosition()
    {
        var project = NewProject();
        _spineService.AddChapter(project, "One", "chapters/one.md");
        _spineService.AddChapter(project, "Two", "chapters/two.md");

        var inserted = _spineService.AddChapter(project, "New First", "chapters/three.md", positionHint: 1);

        var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["New First", "One", "Two"], chapters.Select(c => c.Title));
        Assert.Equal(1, inserted.ResolvedNumber);
    }

    [Fact]
    public void AddChapter_WithPositionHintBeyondCurrentCount_AppendsAtEnd()
    {
        var project = NewProject();
        _spineService.AddChapter(project, "One", "chapters/one.md");

        _spineService.AddChapter(project, "Way Later", "chapters/two.md", positionHint: 99);

        var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["One", "Way Later"], chapters.Select(c => c.Title));
    }
}

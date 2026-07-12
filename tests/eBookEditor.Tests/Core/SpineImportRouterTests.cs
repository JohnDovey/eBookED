using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class SpineImportRouterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly SpineImportRouter _router;

    public SpineImportRouterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _router = new SpineImportRouter(_spineService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EbookProject BuildSampleProject()
    {
        var metadata = new BookMetadata { Title = "Import Router Test", CopyrightHolder = "Jane Author", Language = "en" };
        return _projectService.CreateProject(_tempDir, "Import Router Test", metadata);
    }

    [Fact]
    public void AddImportedItemToSpine_TitleCollidesWithTheGeneratedAboutTheAuthorPage_RenamesTheImportedOne()
    {
        // Every project already has an "About the Author" page seeded at creation (see
        // ProjectService.CreateProject) — importing a document with its own "About the Author"
        // heading (classified as back matter by SpecialPageClassifier) must not create a second,
        // confusingly-identical-looking entry.
        var project = BuildSampleProject();
        Assert.Single(project.Spine, i => i.Title == "About the Author");

        var imported = _router.AddImportedItemToSpine(project, "About the Author", "backmatter/imported-bio.ebhtml", SpineItemType.BackMatter, ChapterNumberMode.Auto);

        Assert.Equal("About the Author (Imported)", imported.Title);
        Assert.Single(project.Spine, i => i.Title == "About the Author");
        Assert.Single(project.Spine, i => i.Title == "About the Author (Imported)");
    }

    [Fact]
    public void AddImportedItemToSpine_TitleCollidesWithTheGeneratedIndexPage_RenamesTheImportedOne()
    {
        var project = BuildSampleProject();
        var indexPath = Path.Combine(project.BackMatterDir, ProjectPaths.IndexPageFileName);
        File.WriteAllText(indexPath, "");
        _spineService.AddBackMatterItem(project, "Index", $"{ProjectPaths.BackMatterDirName}/{ProjectPaths.IndexPageFileName}");

        var imported = _router.AddImportedItemToSpine(project, "Index", "backmatter/imported-index.ebhtml", SpineItemType.BackMatter, ChapterNumberMode.Auto);

        Assert.Equal("Index (Imported)", imported.Title);
    }

    [Fact]
    public void AddImportedItemToSpine_NoExistingGeneratedPageWithThatTitle_UsesTheTitleAsIs()
    {
        // A brand-new project has no generated Index page until "Generate/Regenerate Index" has
        // been run at least once — importing a document titled "Index" before that happens
        // should use the plain title, not pre-emptively disambiguate against a page that
        // doesn't exist yet.
        var project = BuildSampleProject();

        var imported = _router.AddImportedItemToSpine(project, "Index", "backmatter/imported-index.ebhtml", SpineItemType.BackMatter, ChapterNumberMode.Auto);

        Assert.Equal("Index", imported.Title);
    }

    [Fact]
    public void AddImportedItemToSpine_OrdinaryChapterTitle_IsUnaffected()
    {
        var project = BuildSampleProject();

        var imported = _router.AddImportedItemToSpine(project, "Chapter One", "chapters/chapter-one.ebhtml", SpineItemType.Chapter, ChapterNumberMode.Auto);

        Assert.Equal("Chapter One", imported.Title);
    }
}

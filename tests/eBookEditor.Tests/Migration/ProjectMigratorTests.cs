using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Migration.Services;

namespace eBookEditor.Tests.Migration;

public class ProjectMigratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly ProjectMigrator _migrator = new();

    public ProjectMigratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>Builds a project the way ProjectService.CreateProject always has (front matter/
    /// back matter files start empty), then hand-writes legacy ".md" content into them and adds
    /// a Markdown chapter — simulating a real project saved by an app version that predates the
    /// HTML content-model refactor, back when ProjectPaths pointed generated pages/chapters at
    /// ".md".</summary>
    private EbookProject BuildLegacyProject()
    {
        var metadata = new BookMetadata { Title = "Legacy Book" };
        var project = _projectService.CreateProject(_tempDir, "Legacy Book", metadata);

        RewriteAsLegacy(project, SpineItemType.FrontMatter, 0, "title-page.md", "# Legacy Book\n");
        RewriteAsLegacy(project, SpineItemType.FrontMatter, 1, "copyright.md", "Copyright text.\n");
        RewriteAsLegacy(project, SpineItemType.FrontMatter, 2, "toc.md", "# Table of Contents\n");
        RewriteAsLegacy(project, SpineItemType.BackMatter, 0, "about-the-author.md", "Bio text.\n");

        var chapterPath = Path.Combine(project.ChaptersDir, "001 - Chapter One.md");
        File.WriteAllText(chapterPath, "---\ntitle: Chapter One\n---\n\nHello **world**.\n");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        var chapterItem = _spineService.AddChapter(project, "Chapter One", relativePath);
        project.Spine[project.Spine.FindIndex(i => i.Id == chapterItem.Id)] = chapterItem with { RelativePath = relativePath };

        File.WriteAllText(Path.Combine(project.DirectoryPath, "book.md"), "# Legacy Book\n");
        _projectService.SaveProject(project);
        return project;
    }

    private static void RewriteAsLegacy(EbookProject project, SpineItemType type, int indexWithinType, string legacyFileName, string body)
    {
        var dir = type switch
        {
            SpineItemType.FrontMatter => project.FrontMatterDir,
            SpineItemType.BackMatter => project.BackMatterDir,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        var subDirName = type == SpineItemType.FrontMatter ? ProjectPaths.FrontMatterDirName : ProjectPaths.BackMatterDirName;

        var legacyPath = Path.Combine(dir, legacyFileName);
        File.WriteAllText(legacyPath, body);

        var items = project.Spine.Where(i => i.Type == type).OrderBy(i => i.Order).ToList();
        var item = items[indexWithinType];
        var oldPath = project.ResolvePath(item);
        if (File.Exists(oldPath) && oldPath != legacyPath)
            File.Delete(oldPath);

        var index = project.Spine.FindIndex(i => i.Id == item.Id);
        project.Spine[index] = item with { RelativePath = $"{subDirName}/{legacyFileName}" };
    }

    [Fact]
    public void NeedsMigration_TrueForALegacyProject_FalseForACurrentProject()
    {
        var legacyProject = BuildLegacyProject();
        Assert.True(_migrator.NeedsMigration(legacyProject));

        var currentProject = _projectService.CreateProject(_tempDir, "Current Book", new BookMetadata { Title = "Current Book" });
        Assert.False(_migrator.NeedsMigration(currentProject));
    }

    [Fact]
    public void MigrateToHtml_ConvertsEveryLegacyFileToHtmlAndDeletesTheOriginal()
    {
        var project = BuildLegacyProject();

        var result = _migrator.MigrateToHtml(project);

        Assert.Equal(5, result.ConvertedFileCount); // 3 front matter + 1 back matter + 1 chapter
    }

    [Fact]
    public void MigrateToHtml_ConvertsTheChapterBodyFromMarkdownToRealHtml()
    {
        var project = BuildLegacyProject();

        _migrator.MigrateToHtml(project);

        var chapterItem = project.Spine.Single(i => i.Type == SpineItemType.Chapter);
        Assert.EndsWith(".ebhtml", chapterItem.RelativePath);
        Assert.False(File.Exists(Path.Combine(project.DirectoryPath, "chapters", "001 - Chapter One.md")));

        var (_, body) = _chapterFileService.ReadChapter(project.ResolvePath(chapterItem));
        Assert.Contains("<strong>world</strong>", body);
    }

    [Fact]
    public void MigrateToHtml_ConvertsHandEditedGeneratedPagesVerbatim_DoesNotRegenerateFromMetadata()
    {
        // The core Phase 6 requirement: a generated page a user hand-edited must survive the
        // upgrade with its actual (converted) content, not get silently overwritten by a fresh
        // PageGeneratorService.RegenerateAllGeneratedPages render from metadata.
        var project = BuildLegacyProject();
        var copyrightItem = project.Spine.Single(i => i.RelativePath.EndsWith("copyright.md", StringComparison.Ordinal));
        File.WriteAllText(project.ResolvePath(copyrightItem), "A hand-written imprint page with **custom** wording.\n");

        _migrator.MigrateToHtml(project);

        var updatedItem = project.Spine.Single(i => i.Id == copyrightItem.Id);
        var (_, body) = _chapterFileService.ReadChapter(project.ResolvePath(updatedItem));
        Assert.Contains("A hand-written imprint page with <strong>custom</strong> wording.", body);
    }

    [Fact]
    public void MigrateToHtml_SetsSchemaVersionToCurrentAndRegeneratesTheBookIndex()
    {
        var project = BuildLegacyProject();

        _migrator.MigrateToHtml(project);

        Assert.Equal(ProjectFile.CurrentSchemaVersion, project.ProjectFile.SchemaVersion);
        Assert.True(File.Exists(project.BookMdPath));
        Assert.False(File.Exists(Path.Combine(project.DirectoryPath, "book.md")));
        Assert.Contains("Legacy Book", File.ReadAllText(project.BookMdPath));
    }

    [Fact]
    public void MigrateToHtml_IsIdempotent_SecondCallConvertsNothing()
    {
        var project = BuildLegacyProject();
        _migrator.MigrateToHtml(project);

        var secondResult = _migrator.MigrateToHtml(project);

        Assert.Equal(0, secondResult.ConvertedFileCount);
        Assert.False(_migrator.NeedsMigration(project));
    }

    [Fact]
    public void MigrateToHtml_NormalizesChapterFileNamesToTheCurrentNoSpaceConvention()
    {
        var project = BuildLegacyProject();

        _migrator.MigrateToHtml(project);

        var chapterItem = project.Spine.Single(i => i.Type == SpineItemType.Chapter);
        Assert.DoesNotContain(' ', Path.GetFileName(chapterItem.RelativePath));
        Assert.True(File.Exists(project.ResolvePath(chapterItem)));
    }

    [Fact]
    public void CreateBackup_CopiesTheWholeProjectDirectory()
    {
        var project = BuildLegacyProject();

        var backupDir = _migrator.CreateBackup(project);

        Assert.True(Directory.Exists(backupDir));
        Assert.True(File.Exists(Path.Combine(backupDir, "project.ebookproj.json")));
        Assert.True(File.Exists(Path.Combine(backupDir, "chapters", "001 - Chapter One.md")));

        // The backup must be untouched by a subsequent migration of the real project.
        _migrator.MigrateToHtml(project);
        Assert.True(File.Exists(Path.Combine(backupDir, "chapters", "001 - Chapter One.md")));
    }

    [Fact]
    public void CreateBackup_UsesAUniqueNameIfABackupAlreadyExists()
    {
        var project = BuildLegacyProject();

        var firstBackup = _migrator.CreateBackup(project);
        var secondBackup = _migrator.CreateBackup(project);

        Assert.NotEqual(firstBackup, secondBackup);
        Assert.True(Directory.Exists(firstBackup));
        Assert.True(Directory.Exists(secondBackup));
    }
}

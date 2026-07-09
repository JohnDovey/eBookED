using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Markdown;

public class MarkdownExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly MarkdownExportService _exportService = new();

    public MarkdownExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EbookProject BuildProjectWithChapter()
    {
        var metadata = new BookMetadata { Title = "Export Test Book" };
        var project = _projectService.CreateProject(_tempDir, "Export Test Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "The Arrival");
        var relPath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "The Arrival", Subtitle = "A new beginning" },
            "Once upon a time...");
        var item = _spineService.AddChapter(project, "The Arrival", relPath);
        item.Subtitle = "A new beginning";

        return project;
    }

    [Fact]
    public void ExportChapter_SynthesizesHeadingFromTitleAndSubtitle()
    {
        var project = BuildProjectWithChapter();
        var chapter = project.Spine.Single(i => i.Type == SpineItemType.Chapter);

        var markdown = _exportService.ExportChapter(project, chapter);

        Assert.Contains("# Chapter 1: The Arrival", markdown);
        Assert.Contains("## A new beginning", markdown);
        Assert.Contains("Once upon a time...", markdown);
    }

    [Fact]
    public void ExportWholeBook_ConcatenatesAllSectionsInSpineOrderWithSeparators()
    {
        var project = BuildProjectWithChapter();

        var markdown = _exportService.ExportWholeBook(project);

        Assert.Contains("Export Test Book", markdown);
        Assert.Contains("# Chapter 1: The Arrival", markdown);
        Assert.Contains("Once upon a time...", markdown);

        var titleIndex = markdown.IndexOf("Export Test Book", StringComparison.Ordinal);
        var chapterIndex = markdown.IndexOf("Chapter 1: The Arrival", StringComparison.Ordinal);
        Assert.True(titleIndex < chapterIndex);
        Assert.Contains("---", markdown);
    }

    [Fact]
    public void ExportChapter_OmitsSubheadingWhenNoneSet()
    {
        var project = BuildProjectWithChapter();
        var chapter = project.Spine.Single(i => i.Type == SpineItemType.Chapter);
        chapter.Subtitle = null;

        var markdown = _exportService.ExportChapter(project, chapter);

        Assert.DoesNotContain("##", markdown);
    }
}

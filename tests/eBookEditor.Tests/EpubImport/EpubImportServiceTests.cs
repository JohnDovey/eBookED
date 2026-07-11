using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.EpubImport.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.EpubImport;

public class EpubImportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly EpubBuilder _epubBuilder = new();
    private readonly EpubImportService _epubImportService = new();

    public EpubImportServiceTests()
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
            Title = "Round Trip Test Book",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            CopyrightYear = 2026,
            Isbn13 = "9780306406157",
            Language = "en"
        };
        var project = _projectService.CreateProject(_tempDir, "Round Trip Test Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);

        var chapterOnePath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "First Chapter");
        _chapterFileService.WriteChapter(chapterOnePath, new ChapterFrontMatter { Title = "First Chapter" }, "<p>Body of the first chapter.</p>");
        _spineService.AddChapter(project, "First Chapter", Path.GetRelativePath(project.DirectoryPath, chapterOnePath).Replace('\\', '/'));

        var chapterTwoPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Second Chapter");
        _chapterFileService.WriteChapter(chapterTwoPath, new ChapterFrontMatter { Title = "Second Chapter" }, "<p>Body of the second chapter.</p>");
        _spineService.AddChapter(project, "Second Chapter", Path.GetRelativePath(project.DirectoryPath, chapterTwoPath).Replace('\\', '/'));

        _chapterFileService.SyncChapterFileNames(project);
        File.WriteAllText(project.BookMdPath, new BookIndexGenerator().GenerateBookMd(project));
        _projectService.SaveProject(project);
        return project;
    }

    [Fact]
    public void Import_RoundTripsThisAppsOwnExportedEpub_MetadataAndChaptersSurvive()
    {
        var project = BuildSampleProject();
        var epubPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, epubPath);

        var result = _epubImportService.Import(epubPath);

        Assert.Equal("Round Trip Test Book", result.Metadata.Title);
        Assert.Equal("en", result.Metadata.Language);
        Assert.Contains(result.Metadata.Contributors, c => c.Name == "Jane Author" && c.Role == ContributorRole.Author);
        Assert.Equal("9780306406157", result.Metadata.Isbn13);

        var chapters = result.Items.Where(i => i.Type == SpineItemType.Chapter).ToList();
        Assert.Equal(2, chapters.Count);
        Assert.Contains(chapters, c => c.Title == "First Chapter" && c.Body.Contains("Body of the first chapter."));
        Assert.Contains(chapters, c => c.Title == "Second Chapter" && c.Body.Contains("Body of the second chapter."));

        // Round-tripping this app's own export must not double up the "Chapter N: " prefix
        // that gets resynthesized at render time from ResolvedNumber.
        Assert.DoesNotContain(chapters, c => c.Title.StartsWith("Chapter ", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_SkipsTheSourcesOwnTableOfContentsPage()
    {
        var project = BuildSampleProject();
        var epubPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, epubPath);

        var result = _epubImportService.Import(epubPath);

        Assert.DoesNotContain(result.Items, i => i.Title == "Table of Contents");
    }

    [Fact]
    public void Import_ExtractsTheCoverImage()
    {
        var project = BuildSampleProject();
        var coverBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var coverPath = Path.Combine(project.ImagesDir, "cover.jpg");
        File.WriteAllBytes(coverPath, coverBytes);
        project.ProjectFile.Metadata = project.Metadata with { CoverImagePath = "images/cover.jpg" };
        _projectService.SaveProject(project);

        var epubPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, epubPath);

        var result = _epubImportService.Import(epubPath);

        Assert.Equal(coverBytes, result.CoverImageBytes);
        Assert.Equal("cover.jpg", result.CoverImageFileName);
    }

    [Fact]
    public void Import_ExtractsAnEmbeddedChapterImage()
    {
        var project = BuildSampleProject();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        File.WriteAllBytes(Path.Combine(project.ImagesDir, "illustration.png"), imageBytes);

        var chapterPath = project.ResolvePath(project.Spine.First(i => i.Title == "First Chapter"));
        var (frontMatter, _) = _chapterFileService.ReadChapter(chapterPath);
        _chapterFileService.WriteChapter(chapterPath, frontMatter, "<p>Text.</p><img src=\"../images/illustration.png\" alt=\"\">");

        var epubPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, epubPath);

        var result = _epubImportService.Import(epubPath);

        var chapter = result.Items.Single(i => i.Title == "First Chapter");
        var image = Assert.Single(chapter.Images);
        Assert.Equal(imageBytes, image.Bytes);
        Assert.Contains($"../images/{image.FileName}", chapter.Body);
    }
}

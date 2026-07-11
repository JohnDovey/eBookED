using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class DocumentLinkDestinationScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly DocumentLinkDestinationScanner _scanner = new();

    public DocumentLinkDestinationScannerTests()
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
            Title = "Scanner Test Book",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            Language = "en"
        };
        return _projectService.CreateProject(_tempDir, "Scanner Test Book", metadata);
    }

    private SpineItem AddChapter(EbookProject project, string title, string body)
    {
        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, title);
        _chapterFileService.WriteChapter(chapterPath, new ChapterFrontMatter { Title = title }, body);
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _spineService.AddChapter(project, title, relativePath);
        return project.Spine.Single(i => i.RelativePath == relativePath);
    }

    [Fact]
    public void FindAll_ReturnsNothing_WhenNoChapterHasAMarkedDestination()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One", "<p>Plain text, nothing marked.</p>");

        var destinations = _scanner.FindAll(project);

        Assert.Empty(destinations);
    }

    [Fact]
    public void FindAll_FindsADestinationSpanAndUsesItsTextAsTheLabel()
    {
        var project = BuildSampleProject();
        var chapter = AddChapter(project, "Chapter One",
            "<p>Meet <span id=\"dest:the-captain-a1b2c3\">Captain Reyes</span> for the first time.</p>");

        var destinations = _scanner.FindAll(project);

        var destination = Assert.Single(destinations);
        Assert.Equal(chapter.RelativePath, destination.Item.RelativePath);
        Assert.Equal("dest:the-captain-a1b2c3", destination.DestinationId);
        Assert.Equal("Captain Reyes", destination.Label);
    }

    [Fact]
    public void FindAll_ScansAllSpineItemsAndSkipsUnmarkedSpans()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One", "<p><span class=\"small-caps\">Not a destination.</span></p>");
        AddChapter(project, "Chapter Two", "<p><span id=\"dest:second-a9f8e7\">Second chapter marker</span></p>");

        var destinations = _scanner.FindAll(project);

        var destination = Assert.Single(destinations);
        Assert.Equal("Second chapter marker", destination.Label);
    }
}

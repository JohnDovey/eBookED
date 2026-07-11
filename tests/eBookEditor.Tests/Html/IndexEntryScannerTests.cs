using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class IndexEntryScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly IndexEntryScanner _scanner = new();

    public IndexEntryScannerTests()
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
            Title = "Index Scanner Test",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            Language = "en"
        };
        return _projectService.CreateProject(_tempDir, "Index Scanner Test", metadata);
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
    public void FindAll_ReturnsNothing_WhenNoChapterHasAMarkedIndexEntry()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One", "<p>Plain text, nothing marked.</p>");

        Assert.Empty(_scanner.FindAll(project));
    }

    [Fact]
    public void FindAll_FindsAnIndexEntrySpanAndReadsItsTermAttribute()
    {
        var project = BuildSampleProject();
        var chapter = AddChapter(project, "Chapter One",
            "<p>Meet <span class=\"index-entry\" data-index-term=\"Captain Reyes\" id=\"idx:captain-reyes:0\">the captain</span> here.</p>");

        var occurrence = Assert.Single(_scanner.FindAll(project));

        Assert.Equal(chapter.RelativePath, occurrence.Item.RelativePath);
        Assert.Equal("Captain Reyes", occurrence.Term);
        Assert.Equal("idx:captain-reyes:0", occurrence.MarkerId);
    }

    [Fact]
    public void FindAll_FindsMultipleOccurrencesAcrossChapters()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One",
            "<p><span class=\"index-entry\" data-index-term=\"captain\" id=\"idx:captain:0\">captain</span></p>");
        AddChapter(project, "Chapter Two",
            "<p><span class=\"index-entry\" data-index-term=\"captain\" id=\"idx:captain:1\">Captain</span> and " +
            "<span class=\"index-entry\" data-index-term=\"ship\" id=\"idx:ship:0\">ship</span></p>");

        var occurrences = _scanner.FindAll(project);

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(2, occurrences.Count(o => o.Term == "captain"));
        Assert.Single(occurrences, o => o.Term == "ship");
    }
}

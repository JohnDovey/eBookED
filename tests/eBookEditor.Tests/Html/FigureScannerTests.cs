using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class FigureScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly FigureScanner _scanner = new();

    public FigureScannerTests()
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
            Title = "Figure Scanner Test",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            Language = "en"
        };
        return _projectService.CreateProject(_tempDir, "Figure Scanner Test", metadata);
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
    public void FindAll_ReturnsNothing_WhenNoChapterHasAFigure()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One", "<p>Plain text, no images.</p>");

        Assert.Empty(_scanner.FindAll(project));
    }

    [Fact]
    public void FindAll_FindsACaptionedFigureAndReadsItsIdAndCaption()
    {
        var project = BuildSampleProject();
        var chapter = AddChapter(project, "Chapter One",
            """<figure id="fig:abc123" style="text-align:center"><img src="../images/cat.jpg" alt="Cat" width="200" height="150"><figcaption class="caption">A very good cat</figcaption></figure>""");

        var occurrence = Assert.Single(_scanner.FindAll(project));

        Assert.Equal(chapter.RelativePath, occurrence.Item.RelativePath);
        Assert.Equal("fig:abc123", occurrence.FigureId);
        Assert.Equal("A very good cat", occurrence.Caption);
    }

    [Fact]
    public void FindAll_SkipsFiguresWithNoCaption()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One",
            """<figure id="fig:abc123"><img src="../images/cat.jpg" alt="Cat" width="200" height="150"></figure>""");

        Assert.Empty(_scanner.FindAll(project));
    }

    [Fact]
    public void FindAll_BackfillsAMissingIdAndPersistsTheChapterFile()
    {
        // Regression test: figures inserted before this feature existed (or imported) have no
        // "fig:" id at all — the scanner must assign and persist one rather than silently
        // skipping them or re-generating a different id on every scan.
        var project = BuildSampleProject();
        var chapter = AddChapter(project, "Chapter One",
            """<figure><img src="../images/cat.jpg" alt="Cat" width="200" height="150"><figcaption class="caption">Legacy figure</figcaption></figure>""");

        var firstScan = Assert.Single(_scanner.FindAll(project));
        Assert.StartsWith(InternalLinkConvention.FigureIdPrefix, firstScan.FigureId);

        var (_, persistedBody) = _chapterFileService.ReadChapter(project.ResolvePath(chapter));
        Assert.Contains(firstScan.FigureId, persistedBody);

        var secondScan = Assert.Single(_scanner.FindAll(project));
        Assert.Equal(firstScan.FigureId, secondScan.FigureId);
    }

    [Fact]
    public void FindAll_FindsMultipleFiguresAcrossChapters()
    {
        var project = BuildSampleProject();
        AddChapter(project, "Chapter One",
            """<figure id="fig:one"><img src="../images/a.jpg" alt="A"><figcaption class="caption">First</figcaption></figure>""");
        AddChapter(project, "Chapter Two",
            """<figure id="fig:two"><img src="../images/b.jpg" alt="B"><figcaption class="caption">Second</figcaption></figure>""");

        var occurrences = _scanner.FindAll(project);

        Assert.Equal(2, occurrences.Count);
        Assert.Contains(occurrences, o => o.Caption == "First");
        Assert.Contains(occurrences, o => o.Caption == "Second");
    }
}

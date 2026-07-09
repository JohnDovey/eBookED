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
        var path = Path.Combine(_tempDir, "chapter.md");
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
}

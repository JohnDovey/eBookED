using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Markdown;

public class BookIndexGeneratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();

    public BookIndexGeneratorTests()
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
    public void GenerateBookMd_GroupsItemsByTypeInSpineOrder()
    {
        var project = _projectService.CreateProject(_tempDir, "Index Test", new BookMetadata { Title = "Index Test" });
        File.WriteAllText(Path.Combine(project.ChaptersDir, "one.md"), "One");
        File.WriteAllText(Path.Combine(project.ChaptersDir, "two.md"), "Two");
        _spineService.AddChapter(project, "First Chapter", "chapters/one.md");
        _spineService.AddChapter(project, "Second Chapter", "chapters/two.md");

        var bookMd = _bookIndexGenerator.GenerateBookMd(project);

        Assert.Contains("## Front Matter", bookMd);
        Assert.Contains("## Chapters", bookMd);
        Assert.Contains("## Back Matter", bookMd);
        Assert.Contains("- [Chapter 1: First Chapter](chapters/one.md)", bookMd);
        Assert.Contains("- [Chapter 2: Second Chapter](chapters/two.md)", bookMd);

        var firstIndex = bookMd.IndexOf("Chapter 1", StringComparison.Ordinal);
        var secondIndex = bookMd.IndexOf("Chapter 2", StringComparison.Ordinal);
        Assert.True(firstIndex < secondIndex);
    }
}

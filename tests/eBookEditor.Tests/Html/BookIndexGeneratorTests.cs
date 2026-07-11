using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

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
        File.WriteAllText(Path.Combine(project.ChaptersDir, "one.ebhtml"), "One");
        File.WriteAllText(Path.Combine(project.ChaptersDir, "two.ebhtml"), "Two");
        _spineService.AddChapter(project, "First Chapter", "chapters/one.ebhtml");
        _spineService.AddChapter(project, "Second Chapter", "chapters/two.ebhtml");

        var html = _bookIndexGenerator.GenerateBookMd(project);

        Assert.Contains("<h2>Front Matter</h2>", html);
        Assert.Contains("<h2>Chapters</h2>", html);
        Assert.Contains("<h2>Back Matter</h2>", html);
        Assert.Contains("<li><a href=\"chapters/one.ebhtml\">Chapter 1: First Chapter</a></li>", html);
        Assert.Contains("<li><a href=\"chapters/two.ebhtml\">Chapter 2: Second Chapter</a></li>", html);

        var firstIndex = html.IndexOf("Chapter 1", StringComparison.Ordinal);
        var secondIndex = html.IndexOf("Chapter 2", StringComparison.Ordinal);
        Assert.True(firstIndex < secondIndex);
    }

    [Fact]
    public void GenerateBookMd_DividerAndCustomMatterItemsRenderUnnumbered()
    {
        var project = _projectService.CreateProject(_tempDir, "Index Test", new BookMetadata { Title = "Index Test" });
        File.WriteAllText(Path.Combine(project.ChaptersDir, "one.ebhtml"), "One");
        File.WriteAllText(Path.Combine(project.ChaptersDir, "divider.ebhtml"), "Divider");
        _spineService.AddChapter(project, "First Chapter", "chapters/one.ebhtml");
        _spineService.AddChapterDivider(project, "Part Two", "chapters/divider.ebhtml");
        _spineService.AddFrontMatterItem(project, "Acknowledgements", "frontmatter/acknowledgements.ebhtml");

        var html = _bookIndexGenerator.GenerateBookMd(project);

        Assert.Contains("<li><a href=\"chapters/divider.ebhtml\">Part Two</a></li>", html);
        Assert.DoesNotContain("Chapter Part Two", html);
        Assert.Contains("<li><a href=\"frontmatter/acknowledgements.ebhtml\">Acknowledgements</a></li>", html);
    }
}

using eBookEditor.ChapterImport.Services;
using eBookEditor.Tests.DocxImport;

namespace eBookEditor.Tests.ChapterImport;

public class ChapterImportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ChapterImportService _service = new();

    public ChapterImportServiceTests()
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
    public void ImportFile_LegacyMarkdown_UsesFileNameHintForTitleAndPosition()
    {
        var path = Path.Combine(_tempDir, "23. What Now.md");
        File.WriteAllText(path, "The body of the chapter.");

        var drafts = _service.ImportFile(path);

        var draft = Assert.Single(drafts);
        Assert.Equal("What Now", draft.Title);
        Assert.Equal(23, draft.PositionHint);
        Assert.Equal("The body of the chapter.", draft.Body);
    }

    [Fact]
    public void ImportFile_Ebhtml_UsesFileNameHintForTitleAndPosition()
    {
        var path = Path.Combine(_tempDir, "23. What Now.ebhtml");
        File.WriteAllText(path, "<p>The body of the chapter.</p>");

        var drafts = _service.ImportFile(path);

        var draft = Assert.Single(drafts);
        Assert.Equal("What Now", draft.Title);
        Assert.Equal(23, draft.PositionHint);
        Assert.Equal("<p>The body of the chapter.</p>", draft.Body);
    }

    [Fact]
    public void ImportFile_Markdown_PrefersFrontMatterTitleOverFileNameHint()
    {
        var path = Path.Combine(_tempDir, "5. Placeholder.md");
        File.WriteAllText(path, "---\ntitle: Real Title\n---\n\nBody text.");

        var draft = Assert.Single(_service.ImportFile(path));

        Assert.Equal("Real Title", draft.Title);
        Assert.Equal(5, draft.PositionHint);
    }

    [Fact]
    public void ImportFile_Html_SanitizesAndUsesFileNameHint()
    {
        var path = Path.Combine(_tempDir, "12 - The Arrival.html");
        File.WriteAllText(path, "<h1>Ignored</h1><p>Hello <strong>world</strong>.</p>");

        var draft = Assert.Single(_service.ImportFile(path));

        Assert.Equal("The Arrival", draft.Title);
        Assert.Equal(12, draft.PositionHint);
        Assert.Contains("<strong>world</strong>", draft.Body);
    }

    [Fact]
    public void ImportFile_Html_StripsScriptTagsAndEventHandlerAttributes()
    {
        var path = Path.Combine(_tempDir, "1. Untrusted.html");
        File.WriteAllText(path, "<p onclick=\"alert('x')\">Hi</p><script>alert('x')</script>");

        var draft = Assert.Single(_service.ImportFile(path));

        Assert.DoesNotContain("<script", draft.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", draft.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hi", draft.Body);
    }

    [Fact]
    public void ImportFile_SingleChapterDocx_AppliesFileNamePositionHint()
    {
        var path = Path.Combine(_tempDir, "7. Solo Chapter.docx");
        DocxFixtureBuilder.BuildDocxWithHyperlink(path);

        var draft = Assert.Single(_service.ImportFile(path));

        Assert.Equal(7, draft.PositionHint);
    }

    [Fact]
    public void ImportFile_MultiChapterDocx_SplitsByHeadingAndDropsPositionHint()
    {
        var path = Path.Combine(_tempDir, "3. Ignored Name.docx");
        DocxFixtureBuilder.BuildSimpleDocx(path);

        var drafts = _service.ImportFile(path);

        Assert.Equal(3, drafts.Count);
        Assert.Equal(["Chapter One", "Chapter Two", "Chapter 3: The Finale"], drafts.Select(d => d.Title));
        Assert.All(drafts, d => Assert.Null(d.PositionHint));
    }

    [Fact]
    public void ImportFile_UnsupportedExtension_Throws()
    {
        var path = Path.Combine(_tempDir, "cover.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);

        Assert.Throws<NotSupportedException>(() => _service.ImportFile(path));
    }
}

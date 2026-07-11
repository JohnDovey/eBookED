using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.EpubImport.Services;

namespace eBookEditor.Tests.EpubImport;

/// <summary>End-to-end coverage of the three previously-scope-limited behaviors together,
/// against a hand-built EPUB from a producer other than this app (see
/// ForeignEpubFixtureBuilder) — stresses the importer against unfamiliar markup shapes rather
/// than only round-tripping this app's own conventions.</summary>
public class EpubProjectImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;
    private readonly string _destinationDir;
    private readonly TemplateService _templateService;
    private readonly FontService _fontService;
    private readonly ChapterFileService _chapterFileService = new();

    public EpubProjectImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        _sourceDir = Path.Combine(_tempDir, "source");
        _destinationDir = Path.Combine(_tempDir, "destination");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destinationDir);
        _templateService = new TemplateService(Path.Combine(_tempDir, "templates"));
        _fontService = new FontService(Path.Combine(_tempDir, "fonts"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Import_ForeignEpub_CreatesATemplateFromItsCss_RewritesTheCrossChapterLink_AndConvertsTheFootnote()
    {
        var epubPath = ForeignEpubFixtureBuilder.Build(Path.Combine(_sourceDir, "foreign.epub"));
        var importer = new EpubProjectImporter(_templateService, _fontService);

        var project = importer.Import(epubPath, _destinationDir, "Foreign Test Book");

        // 1. A new template was created from the source's own CSS and selected.
        Assert.False(string.IsNullOrWhiteSpace(project.Metadata.SelectedTemplate));
        var templateCss = _templateService.GetTemplateCss(project.Metadata.SelectedTemplate);
        Assert.Contains("Georgia Foreign", templateCss);

        // 2. The cross-chapter link now points at chapter two's real project-relative path.
        var chapterOne = project.Spine.Single(i => i.Title == "Chapter One");
        var chapterTwo = project.Spine.Single(i => i.Title == "Chapter Two");
        var (_, chapterOneBody) = _chapterFileService.ReadChapter(project.ResolvePath(chapterOne));
        Assert.Contains($"href=\"{chapterTwo.RelativePath}\"", chapterOneBody);
        Assert.DoesNotContain("chapter2.xhtml", chapterOneBody);

        // 3. The footnote was converted to this app's own convention.
        Assert.Contains("fnref:1", chapterOneBody);
        Assert.Contains("class=\"footnote-ref\"", chapterOneBody);
        Assert.Contains("This is the footnote text.", chapterOneBody);
        Assert.Contains("class=\"footnotes\"", chapterOneBody);
    }

    [Fact]
    public void Import_ForeignEpub_FragmentLinkIntoAnotherChaptersHeading_BecomesARealDestLink()
    {
        var epubPath = ForeignEpubFixtureBuilder.Build(Path.Combine(_sourceDir, "foreign.epub"));
        var importer = new EpubProjectImporter(_templateService, _fontService);

        var project = importer.Import(epubPath, _destinationDir, "Foreign Test Book");

        var chapterOne = project.Spine.Single(i => i.Title == "Chapter One");
        var chapterTwo = project.Spine.Single(i => i.Title == "Chapter Two");
        var (_, chapterOneBody) = _chapterFileService.ReadChapter(project.ResolvePath(chapterOne));
        var (_, chapterTwoBody) = _chapterFileService.ReadChapter(project.ResolvePath(chapterTwo));

        // The source EPUB's "chapter2.xhtml#section-a" fragment link resolves to a real dest:
        // link since chapter2 really does have an "id=section-a" element — not dropped, and not
        // a link to a fragment that doesn't exist.
        var destIdMatch = System.Text.RegularExpressions.Regex.Match(chapterTwoBody, "id=\"(dest:[^\"]+)\"");
        Assert.True(destIdMatch.Success, $"Expected chapter two to contain a retargeted dest: id. Body was:\n{chapterTwoBody}");
        var destId = destIdMatch.Groups[1].Value;

        Assert.Contains($"href=\"{chapterTwo.RelativePath}#{destId}\"", chapterOneBody);
        Assert.DoesNotContain("id=\"section-a\"", chapterTwoBody);

        // The footnote target's own id must survive untouched — it's a same-page fragment too,
        // but must not be folded into the generic dest: convention.
        Assert.Contains("id=\"fn:1\"", chapterOneBody);
    }
}

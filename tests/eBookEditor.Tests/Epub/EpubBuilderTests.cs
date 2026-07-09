using System.IO.Compression;
using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Epub;

public class EpubBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly EpubBuilder _epubBuilder = new();

    public EpubBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private EbookProject BuildSampleProject(string name = "Epub Test Book")
    {
        var metadata = new BookMetadata
        {
            Title = name,
            Contributors = [new Contributor("Jane Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            CopyrightYear = 2026,
            Isbn13 = "9780306406157",
            Language = "en"
        };
        var project = _projectService.CreateProject(_tempDir, name, metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);

        var chapterPath = _chapterFileService.CreateNewChapterFile(project.ChaptersDir, "Chapter One");
        var relativePath = Path.GetRelativePath(project.DirectoryPath, chapterPath).Replace('\\', '/');
        _chapterFileService.WriteChapter(chapterPath,
            new ChapterFrontMatter { Title = "Chapter One" },
            "Hello **world**, this is the first chapter.");
        _spineService.AddChapter(project, "Chapter One", relativePath);

        File.WriteAllText(project.BookMdPath, new BookIndexGenerator().GenerateBookMd(project));
        _projectService.SaveProject(project);
        return project;
    }

    [Fact]
    public void Build_ProducesStructurallyValidEpub()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        Assert.True(File.Exists(outputPath));
        var result = EpubValidationHelper.Validate(outputPath);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Build_MimetypeIsFirstEntryAndStoredUncompressed()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var first = archive.Entries[0];
        Assert.Equal("mimetype", first.FullName);
        Assert.Equal(first.Length, first.CompressedLength);
    }

    [Fact]
    public void Build_SpineContainsAllContentDocsInOrder()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var opfEntry = archive.GetEntry("OEBPS/package.opf")!;
        using var reader = new StreamReader(opfEntry.Open());
        var opfXml = XDocument.Parse(reader.ReadToEnd());

        XNamespace opf = "http://www.idpf.org/2007/opf";
        var itemRefs = opfXml.Descendants(opf + "itemref").ToList();
        Assert.Equal(project.Spine.Count, itemRefs.Count);
    }

    [Fact]
    public void Build_UsesIsbn13AsPackageIdentifier()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var opfEntry = archive.GetEntry("OEBPS/package.opf")!;
        using var reader = new StreamReader(opfEntry.Open());
        var opfXml = XDocument.Parse(reader.ReadToEnd());

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        var identifier = opfXml.Descendants(dc + "identifier").First().Value;
        Assert.Equal("urn:isbn:9780306406157", identifier);
    }

    [Fact]
    public void Build_CopiesReferencedChapterImageIntoPackage()
    {
        var project = BuildSampleProject();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        File.WriteAllBytes(Path.Combine(project.ImagesDir, "diagram.jpg"), imageBytes);

        var chapterItem = project.Spine.Single(i => i.Type == SpineItemType.Chapter);
        var chapterPath = project.ResolvePath(chapterItem);
        var (frontMatter, _) = _chapterFileService.ReadChapter(chapterPath);
        _chapterFileService.WriteChapter(chapterPath, frontMatter, "See the diagram below.\n\n![A diagram](../images/diagram.jpg)");

        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        Assert.NotNull(archive.GetEntry("OEBPS/images/diagram.jpg"));
    }

    [Fact]
    public void Build_FallsBackToUuidIdentifierWhenNoIsbn()
    {
        var metadata = new BookMetadata { Title = "No Isbn Book" };
        var project = _projectService.CreateProject(_tempDir, "No Isbn Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        File.WriteAllText(project.BookMdPath, new BookIndexGenerator().GenerateBookMd(project));

        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var opfEntry = archive.GetEntry("OEBPS/package.opf")!;
        using var reader = new StreamReader(opfEntry.Open());
        var opfXml = XDocument.Parse(reader.ReadToEnd());

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        var identifier = opfXml.Descendants(dc + "identifier").First().Value;
        Assert.StartsWith($"urn:uuid:{metadata.Identifier}", identifier);
    }

    [Fact]
    public void Build_UsesSelectedTemplateCssWhenSet()
    {
        var templatesDir = Path.Combine(_tempDir, "templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(Path.Combine(templatesDir, "Custom.css"), "body { color: purple; }");

        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with { SelectedTemplate = "Custom" };
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        new EpubBuilder(new TemplateService(templatesDir)).Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        using var reader = new StreamReader(archive.GetEntry("OEBPS/styles.css")!.Open());
        Assert.Equal("body { color: purple; }", reader.ReadToEnd());
    }

    [Fact]
    public void Build_FallsBackToDefaultCssWhenNoTemplateSelected()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        using var reader = new StreamReader(archive.GetEntry("OEBPS/styles.css")!.Open());
        Assert.Equal(DefaultStylesheet.Css, reader.ReadToEnd());
    }

    private static XDocument ReadPackageOpf(string epubPath)
    {
        using var archive = ZipFile.OpenRead(epubPath);
        using var reader = new StreamReader(archive.GetEntry("OEBPS/package.opf")!.Open());
        return XDocument.Parse(reader.ReadToEnd());
    }

    [Fact]
    public void Build_EmitsFileAsRefinementWhenContributorHasSortName()
    {
        var metadata = new BookMetadata
        {
            Title = "Sort Name Book",
            Contributors = [new Contributor("John Dovey", ContributorRole.Author, "Dovey, John")]
        };
        var project = _projectService.CreateProject(_tempDir, "Sort Name Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        File.WriteAllText(project.BookMdPath, new BookIndexGenerator().GenerateBookMd(project));
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        var opfXml = ReadPackageOpf(outputPath);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var fileAs = opfXml.Descendants(opf + "meta")
            .SingleOrDefault(m => (string?)m.Attribute("property") == "file-as");
        Assert.NotNull(fileAs);
        Assert.Equal("Dovey, John", fileAs!.Value);
    }

    [Fact]
    public void Build_EmitsAccessibilityMetadataBlock()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        var opfXml = ReadPackageOpf(outputPath);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var properties = opfXml.Descendants(opf + "meta")
            .Select(m => (string?)m.Attribute("property"))
            .Where(p => p is not null)
            .ToList();

        Assert.Contains("schema:accessMode", properties);
        Assert.Contains("schema:accessibilityFeature", properties);
        Assert.Contains("schema:accessibilityHazard", properties);
        Assert.Equal("schema: http://schema.org/", opfXml.Root!.Attribute("prefix")?.Value);
    }

    [Fact]
    public void Build_EmitsGeneratorMetaTag()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        var opfXml = ReadPackageOpf(outputPath);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var generator = opfXml.Descendants(opf + "meta")
            .SingleOrDefault(m => (string?)m.Attribute("name") == "generator");
        Assert.NotNull(generator);
        Assert.StartsWith("eBook Editor", (string?)generator!.Attribute("content"));
    }
}

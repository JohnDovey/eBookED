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
}

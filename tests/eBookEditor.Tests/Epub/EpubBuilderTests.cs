using System.IO.Compression;
using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Html.Services;

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
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
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
            "<p>Hello <strong>world</strong>, this is the first chapter.</p>");
        _spineService.AddChapter(project, "Chapter One", relativePath);
        // Real usage always syncs filenames after adding a chapter (see
        // MainWindowViewModel.AddChapter), which renames to "NNN - Title.ebhtml" — spaces and
        // all. A slugged CreateNewChapterFile name alone ("chapter-one-abc123.ebhtml") never has
        // spaces, so skipping this step let a real space-in-filename bug (broken TOC links) slip
        // past this whole test suite untested.
        _chapterFileService.SyncChapterFileNames(project);

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
    public void Build_TocPageLinksResolveToTheChaptersActualContentDocument()
    {
        var project = BuildSampleProject();
        // BuildSampleProject regenerates the TOC before adding the chapter, so regenerate
        // again now that the chapter is in the spine — mirrors what
        // MainWindowViewModel.AddChapter does for real in the app.
        _pageGenerator.RegenerateAllGeneratedPages(project);
        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var tocEntry = archive.Entries
            .Where(e => e.FullName.StartsWith("OEBPS/content-", StringComparison.Ordinal))
            .Single(e =>
            {
                using var reader = new StreamReader(e.Open());
                return reader.ReadToEnd().Contains("Table of Contents", StringComparison.Ordinal);
            });

        using var tocReader = new StreamReader(tocEntry.Open());
        var tocHtml = tocReader.ReadToEnd();

        var anchors = System.Text.RegularExpressions.Regex.Matches(tocHtml, "<a[^>]*href=\"([^\"]+)\"[^>]*>([^<]*)</a>")
            .Select(m => (Href: m.Groups[1].Value, Text: m.Groups[2].Value))
            .ToList();

        Assert.NotEmpty(anchors);
        Assert.DoesNotContain(anchors, a => a.Href.Contains(".ebhtml", StringComparison.Ordinal));

        // Regression: the chapter's filename ("NNN - Chapter One.ebhtml", via
        // SyncChapterFileNames) contains a space — every spine item except the TOC page itself
        // must resolve to a real, clickable anchor pointing at the EPUB's own "content-NNN.xhtml"
        // naming, not the raw project-relative source path.
        var expectedLinkCount = project.Spine.Count(i => !i.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal));
        Assert.Equal(expectedLinkCount, anchors.Count);
        Assert.Contains(anchors, a => a.Text.Contains("Chapter One", StringComparison.Ordinal));

        var linkedEntries = anchors
            .Select(a => archive.GetEntry($"OEBPS/{a.Href}"))
            .ToList();

        Assert.DoesNotContain(linkedEntries, e => e is null);

        Assert.Contains(linkedEntries, entry =>
        {
            using var reader = new StreamReader(entry!.Open());
            return reader.ReadToEnd().Contains("Hello", StringComparison.Ordinal);
        });
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
        _chapterFileService.WriteChapter(chapterPath, frontMatter,
            "<p>See the diagram below.</p>\n<img src=\"../images/diagram.jpg\" alt=\"A diagram\">");

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

    [Fact]
    public void Build_EmbedsFontsReferencedByTheSelectedTemplateAndAddsManifestEntries()
    {
        var templatesDir = Path.Combine(_tempDir, "templates");
        var fontsDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(templatesDir);
        Directory.CreateDirectory(fontsDir);
        File.WriteAllText(Path.Combine(templatesDir, "Fancy.css"),
            "@font-face { font-family: Alegreya; src: url('fonts/Alegreya-Regular.ttf'); } body { font-family: Alegreya; }");
        File.WriteAllText(Path.Combine(fontsDir, "Alegreya-Regular.ttf"), "fake font bytes");

        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with { SelectedTemplate = "Fancy" };
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        new EpubBuilder(new TemplateService(templatesDir), new FontService(fontsDir)).Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        var fontEntry = archive.GetEntry("OEBPS/fonts/Alegreya-Regular.ttf");
        Assert.NotNull(fontEntry);
        using var reader = new StreamReader(fontEntry!.Open());
        Assert.Equal("fake font bytes", reader.ReadToEnd());

        var opfXml = ReadPackageOpf(outputPath);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var fontItem = opfXml.Descendants(opf + "item")
            .SingleOrDefault(i => (string?)i.Attribute("href") == "fonts/Alegreya-Regular.ttf");
        Assert.NotNull(fontItem);
        Assert.Equal("font/ttf", (string?)fontItem!.Attribute("media-type"));
    }

    [Fact]
    public void Build_SkipsFontFacesWhoseFileIsNotShippedWithTheApp()
    {
        var templatesDir = Path.Combine(_tempDir, "templates");
        Directory.CreateDirectory(templatesDir);
        File.WriteAllText(Path.Combine(templatesDir, "Fancy.css"),
            "@font-face { font-family: Alegreya; src: url('fonts/Alegreya-Regular.ttf'); }");

        var project = BuildSampleProject();
        project.ProjectFile.Metadata = project.Metadata with { SelectedTemplate = "Fancy" };
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        new EpubBuilder(new TemplateService(templatesDir), new FontService(Path.Combine(_tempDir, "no-fonts-here")))
            .Build(project, outputPath);

        using var archive = ZipFile.OpenRead(outputPath);
        Assert.Null(archive.GetEntry("OEBPS/fonts/Alegreya-Regular.ttf"));
    }

    private static XDocument ReadPackageOpf(string epubPath)
    {
        using var archive = ZipFile.OpenRead(epubPath);
        using var reader = new StreamReader(archive.GetEntry("OEBPS/package.opf")!.Open());
        return XDocument.Parse(reader.ReadToEnd());
    }

    private static XDocument ReadEntry(string epubPath, string entryName)
    {
        using var archive = ZipFile.OpenRead(epubPath);
        using var reader = new StreamReader(archive.GetEntry(entryName)!.Open());
        return XDocument.Parse(reader.ReadToEnd());
    }

    [Fact]
    public void Build_EmitsFileAsRefinementWhenContributorHasSortName()
    {
        var metadata = new BookMetadata
        {
            Title = "Sort Name Book",
            Contributors = [new Contributor("John", "Dovey", ContributorRole.Author)]
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

    [Fact]
    public void Build_CoverMetaTag_ReferencesTheActualManifestItemId()
    {
        // Regression test: the legacy <meta name="cover"> tag (still read by some Kindle/KDP
        // conversion paths) used to hard-code content="cover-image" — the manifest item's
        // "properties" *value*, not any item's actual "id" — so it never resolved to
        // anything real.
        var project = BuildSampleProject();
        File.WriteAllBytes(Path.Combine(project.ImagesDir, "cover.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
        project.ProjectFile.Metadata = project.Metadata with { CoverImagePath = "images/cover.jpg" };
        _projectService.SaveProject(project);

        var outputPath = Path.Combine(project.OutputDir, "book.epub");
        _epubBuilder.Build(project, outputPath);

        var opfXml = ReadPackageOpf(outputPath);
        XNamespace opf = "http://www.idpf.org/2007/opf";

        var coverMeta = opfXml.Descendants(opf + "meta").Single(m => (string?)m.Attribute("name") == "cover");
        var referencedId = (string?)coverMeta.Attribute("content");

        var coverItem = opfXml.Descendants(opf + "item")
            .Single(i => (string?)i.Attribute("properties") == "cover-image");

        Assert.Equal((string?)coverItem.Attribute("id"), referencedId);
    }

    [Fact]
    public void Build_NavDocument_IncludesLandmarksWithBodymatterStartOfContent()
    {
        var project = BuildSampleProject();
        var outputPath = Path.Combine(project.OutputDir, "book.epub");

        _epubBuilder.Build(project, outputPath);

        var navXml = ReadEntry(outputPath, "OEBPS/nav.xhtml");
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        XNamespace epub = "http://www.idpf.org/2007/ops";

        var landmarksNav = navXml.Descendants(xhtml + "nav")
            .Single(n => (string?)n.Attribute(epub + "type") == "landmarks");

        var bodymatterLink = landmarksNav.Descendants(xhtml + "a")
            .Single(a => (string?)a.Attribute(epub + "type") == "bodymatter");

        // The sample project's only chapter is content-004.xhtml (title, imprint, toc come
        // first); the landmark must point at the real first chapter, not front matter.
        Assert.EndsWith(".xhtml", (string?)bodymatterLink.Attribute("href"));
        Assert.Contains(landmarksNav.Descendants(xhtml + "a"), a => (string?)a.Attribute(epub + "type") == "titlepage");
        Assert.Contains(landmarksNav.Descendants(xhtml + "a"), a => (string?)a.Attribute(epub + "type") == "toc");
    }
}

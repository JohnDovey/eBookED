using System.IO.Compression;
using System.Text;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Models;

namespace eBookEditor.Epub.Services;

public class EpubBuilder
{
    private readonly ChapterFileService _chapterFileService = new();
    private readonly TemplateService _templateService;
    private readonly FontService _fontService;

    public EpubBuilder(TemplateService? templateService = null, FontService? fontService = null)
    {
        _templateService = templateService ?? new TemplateService();
        _fontService = fontService ?? new FontService();
    }

    public void Build(EbookProject project, string outputPath)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        var metadata = project.Metadata;
        var imagesToCopy = new Dictionary<string, string>();
        var contentDocs = new List<(EpubContentDoc Doc, string Html)>();

        var orderedSpine = project.Spine.OrderBy(i => i.Order).ToList();

        // Assigned in a pass of its own (before any link/image rewriting) so that
        // GenerateTocPage's links — written against each item's project-relative source path,
        // e.g. "chapters/foo-abc123.ebhtml" — can be rewritten to the matching EPUB content
        // document filename regardless of where in the spine order that target falls.
        var contentFileNamesByRelativePath = new Dictionary<string, string>(StringComparer.Ordinal);
        var fileNameIndex = 0;
        foreach (var item in orderedSpine)
        {
            fileNameIndex++;
            contentFileNamesByRelativePath[item.RelativePath] = $"content-{fileNameIndex:D3}.xhtml";
        }

        foreach (var item in orderedSpine)
        {
            var sourcePath = project.ResolvePath(item);
            var rawText = File.ReadAllText(sourcePath);
            var (_, body) = _chapterFileService.ParseChapter(rawText);

            // The stored body is already HTML — no Markdown-to-HTML conversion step needed
            // (that conversion, and this method's own project-relative-source-path/EPUB-
            // content-document-filename link rewriting and image copying, were the only things
            // this class ever did with it beyond wrapping it into the XHTML shell).
            var rewrittenLinks = EpubInternalLinkResolver.RewriteChapterLinks(body, contentFileNamesByRelativePath);
            var sourceDir = Path.GetDirectoryName(sourcePath)!;
            var html = EpubImageResolver.RewriteAndCollectImages(rewrittenLinks, sourceDir, imagesToCopy);

            var fileName = contentFileNamesByRelativePath[item.RelativePath];
            var title = item.Title ?? metadata.Title;
            var epubType = DetermineEpubType(item);

            contentDocs.Add((new EpubContentDoc(item, fileName, epubType, title), html));
        }

        string? coverImageFileName = null;
        if (!string.IsNullOrWhiteSpace(metadata.CoverImagePath))
        {
            var coverAbsPath = Path.Combine(project.DirectoryPath, metadata.CoverImagePath);
            if (File.Exists(coverAbsPath))
                coverImageFileName = EpubImageResolver.RegisterImage(coverAbsPath, imagesToCopy);
        }

        using var stream = new FileStream(outputPath, FileMode.CreateNew);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteStoredEntry(archive, "mimetype", "application/epub+zip");
        WriteEntry(archive, "META-INF/container.xml", EpubContainerWriter.Build());

        foreach (var (doc, html) in contentDocs)
        {
            var xhtml = EpubXhtmlContentWriter.Wrap(doc.Title, doc.EpubType, html);
            WriteEntry(archive, $"OEBPS/{doc.FileName}", xhtml);
        }

        foreach (var (absolutePath, destFileName) in imagesToCopy)
        {
            var entry = archive.CreateEntry($"OEBPS/images/{destFileName}", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(absolutePath);
            fileStream.CopyTo(entryStream);
        }

        var templateCss = _templateService.GetTemplateCss(metadata.SelectedTemplate);
        WriteEntry(archive, "OEBPS/styles.css", templateCss);

        var fontFileNames = _fontService.ParseFontFaces(templateCss)
            .Select(f => f.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(fileName => _fontService.ResolveFontFilePath(fileName) is not null)
            .ToList();

        foreach (var fontFileName in fontFileNames)
        {
            var fontPath = _fontService.ResolveFontFilePath(fontFileName)!;
            var entry = archive.CreateEntry($"OEBPS/fonts/{fontFileName}", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(fontPath);
            fileStream.CopyTo(entryStream);
        }

        var docList = contentDocs.Select(c => c.Doc).ToList();
        WriteEntry(archive, "OEBPS/nav.xhtml", EpubNavDocumentWriter.Build(docList));

        var uniqueIdentifier = ResolveUniqueIdentifier(metadata);
        WriteEntry(archive, "OEBPS/toc.ncx", EpubNcxWriter.Build(metadata.Title, uniqueIdentifier, docList));
        WriteEntry(archive, "OEBPS/package.opf",
            EpubPackageDocumentWriter.Build(metadata, docList, imagesToCopy, fontFileNames, coverImageFileName, uniqueIdentifier));
    }

    private static string ResolveUniqueIdentifier(BookMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.Isbn13))
            return $"urn:isbn:{metadata.Isbn13}";
        if (!string.IsNullOrWhiteSpace(metadata.Isbn10))
            return $"urn:isbn:{metadata.Isbn10}";
        return $"urn:uuid:{metadata.Identifier}";
    }

    private static string DetermineEpubType(SpineItem item)
    {
        if (item.RelativePath.EndsWith(ProjectPaths.TitlePageFileName, StringComparison.Ordinal))
            return "titlepage";
        if (item.RelativePath.EndsWith(ProjectPaths.CopyrightPageFileName, StringComparison.Ordinal))
            return "copyright-page";
        if (item.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal))
            return "toc";
        if (item.RelativePath.EndsWith(ProjectPaths.AboutAuthorPageFileName, StringComparison.Ordinal))
            return "backmatter";
        return item.Type == SpineItemType.Chapter ? "chapter" : "bodymatter";
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteStoredEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }
}

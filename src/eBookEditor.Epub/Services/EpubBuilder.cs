using System.IO.Compression;
using System.Text;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Models;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Epub.Services;

public class EpubBuilder
{
    private readonly MarkdownToHtmlConverter _htmlConverter = new();
    private readonly ChapterFileService _chapterFileService = new();

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
        var index = 0;
        foreach (var item in orderedSpine)
        {
            index++;
            var sourcePath = project.ResolvePath(item);
            var rawText = File.ReadAllText(sourcePath);
            var (_, body) = _chapterFileService.ParseChapter(rawText);

            var sourceDir = Path.GetDirectoryName(sourcePath)!;
            var rewrittenMarkdown = EpubImageResolver.RewriteAndCollectImages(body, sourceDir, imagesToCopy);
            var html = _htmlConverter.ToHtml(rewrittenMarkdown);

            var fileName = $"content-{index:D3}.xhtml";
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

        WriteEntry(archive, "OEBPS/styles.css", DefaultStylesheet.Css);

        var docList = contentDocs.Select(c => c.Doc).ToList();
        WriteEntry(archive, "OEBPS/nav.xhtml", EpubNavDocumentWriter.Build(docList));

        var uniqueIdentifier = ResolveUniqueIdentifier(metadata);
        WriteEntry(archive, "OEBPS/toc.ncx", EpubNcxWriter.Build(metadata.Title, uniqueIdentifier, docList));
        WriteEntry(archive, "OEBPS/package.opf",
            EpubPackageDocumentWriter.Build(metadata, docList, imagesToCopy, coverImageFileName, uniqueIdentifier));
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

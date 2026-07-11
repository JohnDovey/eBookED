using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using eBookEditor.ChapterImport.Models;
using eBookEditor.ChapterImport.Services;
using eBookEditor.Core.Models;
using eBookEditor.DocxImport.Models;
using eBookEditor.DocxImport.Services;
using eBookEditor.EpubImport.Models;

namespace eBookEditor.EpubImport.Services;

/// <summary>Everything read out of a source EPUB, ready to seed a new eBookEditor project —
/// see EpubProjectImporter for the part that actually writes it to disk.</summary>
public record EpubImportResult(
    BookMetadata Metadata,
    IReadOnlyList<ChapterImportDraft> Items,
    byte[]? CoverImageBytes,
    string? CoverImageFileName);

/// <summary>
/// Parses an arbitrary (reasonably well-formed) EPUB file into metadata + a list of chapter
/// drafts, mirroring the shape DocxImportService/ChapterImportService already produce so the
/// same downstream spine-population pipeline (SpineImportRouter) can consume either. Every
/// spine item's XHTML body is sanitized via HtmlImportSanitizer (reused, not duplicated) and
/// classified front/chapter/back-matter via nav landmarks first, falling back to
/// SpecialPageClassifier (also reused from DOCX import — same "Part One"/"Preface"/"Afterword"
/// recognition).
///
/// Deliberate, documented scope limits: the source EPUB's own CSS is never imported (the new
/// project uses this app's own template system); internal cross-chapter hyperlinks are left as
/// inert relative links rather than rewritten (this app's link resolver only understands its
/// own spine paths); footnote markup is imported as plain inline HTML verbatim, not
/// renormalized into this app's specific footnote convention. The source's own table-of-
/// contents and cover-wrapper pages (identified via nav landmarks, not guessed) are skipped
/// entirely — their links/layout would be meaningless in the new project, and title/copyright
/// pages already get freshly generated versions from the parsed metadata.
/// </summary>
public partial class EpubImportService
{
    private readonly HtmlImportSanitizer _sanitizer = new();
    private readonly HtmlParser _htmlParser = new();

    // Strips a synthesized "Chapter N: " prefix (this app's own export format — see
    // ChapterHeadingHtml/EpubNavDocumentWriter) from a title sourced from a chapter's own
    // heading or nav link text, so round-tripping this app's own EPUB back through import
    // doesn't end up with a doubled prefix once the new project resynthesizes it from
    // ResolvedNumber. Only matches when there's a real title after the colon — a bare
    // "Chapter 3" (no title) is left alone, same ambiguity DOCX import's ChapterBoundaryDetector
    // already accepts.
    [GeneratedRegex(@"^(?:Chapter|CHAPTER)\s+(?:\d+|[A-Za-z]+):\s*(.+)$")]
    private static partial Regex ChapterPrefixRegex();

    public EpubImportResult Import(string epubPath)
    {
        using var archive = ZipFile.OpenRead(epubPath);
        var package = EpubPackageReader.Read(archive);
        var navigation = ReadNavigation(archive, package);

        // Landmark types only ever tag a handful of structural waypoints (title page, TOC,
        // first real chapter) — everything else falls back to SpecialPageClassifier's
        // title-based recognition, which defaults to a plain numbered chapter for anything it
        // doesn't recognize. That default is wrong for a book's own title/copyright-style
        // pages sitting before the first real chapter (e.g. this app's own "Imprint" page,
        // which SpecialPageClassifier has no reason to know about) — an unrecognized title
        // there should read as front matter, not a fake chapter. Kept items are filtered/
        // resolved first so this positional rule can be applied in a second pass, once the
        // first "bodymatter"-landmarked item's position is known.
        var kept = new List<(string ItemId, EpubManifestItem ManifestItem, string HrefKey, string? LandmarkType)>();
        foreach (var itemId in package.SpineItemIds)
        {
            if (!package.ManifestById.TryGetValue(itemId, out var manifestItem))
                continue;
            if (!manifestItem.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                continue;

            var hrefKey = StripFragment(manifestItem.Href);
            var landmarkType = navigation.LandmarkTypesByHref.GetValueOrDefault(hrefKey);
            if (landmarkType is "toc" or "cover")
                continue;

            kept.Add((itemId, manifestItem, hrefKey, landmarkType));
        }

        var bodymatterIndex = kept.FindIndex(k => k.LandmarkType == "bodymatter");

        var items = new List<ChapterImportDraft>();
        for (var index = 0; index < kept.Count; index++)
        {
            var (_, manifestItem, hrefKey, landmarkType) = kept[index];

            var entryPath = CombineEpubPath(package.OpfDirectory, manifestItem.Href);
            var entry = archive.GetEntry(entryPath);
            if (entry is null)
                continue;

            string rawHtml;
            using (var entryStream = entry.Open())
            using (var reader = new StreamReader(entryStream))
                rawHtml = reader.ReadToEnd();

            var document = _htmlParser.ParseDocument(rawHtml);
            var body = document.Body ?? document.DocumentElement;

            var images = new List<ExtractedImage>();
            RewriteImages(body, entryPath, archive, images);
            StripEpubNamespaceAttributes(body);
            var sanitizedBody = _sanitizer.Convert(body.InnerHtml);

            var title = StripChapterNumberPrefix(
                navigation.TitlesByHref.GetValueOrDefault(hrefKey)
                ?? ExtractFallbackTitle(document, body)
                ?? "Untitled Chapter");

            var isBeforeFirstChapter = bodymatterIndex >= 0 && index < bodymatterIndex;
            var (type, numberMode) = ClassifyItem(title, landmarkType, isBeforeFirstChapter);
            items.Add(new ChapterImportDraft(title, sanitizedBody, PositionHint: null, images, type, numberMode));
        }

        var (coverBytes, coverFileName) = ExtractCoverImage(archive, package);
        return new EpubImportResult(package.Metadata, items, coverBytes, coverFileName);
    }

    private static EpubNavigationInfo ReadNavigation(ZipArchive archive, EpubPackage package)
    {
        var navItem = package.ManifestById.Values
            .FirstOrDefault(i => i.Properties?.Split(' ').Contains("nav") ?? false);
        if (navItem is not null)
        {
            var navPath = CombineEpubPath(package.OpfDirectory, navItem.Href);
            var navEntry = archive.GetEntry(navPath);
            if (navEntry is not null)
            {
                using var navStream = navEntry.Open();
                return EpubNavigationReader.ReadNav(XDocument.Load(navStream));
            }
        }

        var ncxItem = package.ManifestById.Values
            .FirstOrDefault(i => i.MediaType.Equals("application/x-dtbncx+xml", StringComparison.OrdinalIgnoreCase));
        if (ncxItem is not null)
        {
            var ncxPath = CombineEpubPath(package.OpfDirectory, ncxItem.Href);
            var ncxEntry = archive.GetEntry(ncxPath);
            if (ncxEntry is not null)
            {
                using var ncxStream = ncxEntry.Open();
                return EpubNavigationReader.ReadNcx(XDocument.Load(ncxStream));
            }
        }

        return new EpubNavigationInfo(new Dictionary<string, string>(), new Dictionary<string, string>());
    }

    private static (SpineItemType Type, ChapterNumberMode NumberMode) ClassifyItem(string title, string? landmarkType, bool isBeforeFirstChapter)
    {
        switch (landmarkType)
        {
            case "titlepage" or "copyright-page":
                return (SpineItemType.FrontMatter, ChapterNumberMode.Auto);
            case "backmatter":
                return (SpineItemType.BackMatter, ChapterNumberMode.Auto);
            case "bodymatter":
                return (SpineItemType.Chapter, ChapterNumberMode.Auto);
        }

        var classified = SpecialPageClassifier.Classify(title);
        var isUnrecognizedChapter = classified is (SpineItemType.Chapter, ChapterNumberMode.Auto);
        return isUnrecognizedChapter && isBeforeFirstChapter
            ? (SpineItemType.FrontMatter, ChapterNumberMode.Auto)
            : classified;
    }

    private static void RewriteImages(IElement body, string entryPath, ZipArchive archive, List<ExtractedImage> images)
    {
        var entryDir = Path.GetDirectoryName(entryPath)?.Replace('\\', '/') ?? "";

        foreach (var img in body.QuerySelectorAll("img").ToList())
        {
            var src = img.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(src) ||
                src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var imagePath = CombineEpubPath(entryDir, src);
            var imageEntry = archive.GetEntry(imagePath);
            if (imageEntry is null)
                continue;

            var fileName = UniqueFileName(Path.GetFileName(src), images.Select(i => i.FileName));
            using (var imageStream = imageEntry.Open())
            using (var memoryStream = new MemoryStream())
            {
                imageStream.CopyTo(memoryStream);
                images.Add(new ExtractedImage(fileName, memoryStream.ToArray()));
            }

            img.SetAttribute("src", $"../images/{fileName}");
        }
    }

    private static void StripEpubNamespaceAttributes(IElement body)
    {
        foreach (var element in body.QuerySelectorAll("*").Prepend(body))
        {
            foreach (var attribute in element.Attributes.ToList())
            {
                if (attribute.Name.StartsWith("epub:", StringComparison.OrdinalIgnoreCase) || attribute.Name == "xmlns")
                    element.RemoveAttribute(attribute.Name);
            }
        }
    }

    private static string? ExtractFallbackTitle(IDocument document, IElement body)
    {
        var heading = body.QuerySelector("h1, h2");
        if (heading is not null && !string.IsNullOrWhiteSpace(heading.TextContent))
            return heading.TextContent.Trim();

        return string.IsNullOrWhiteSpace(document.Title) ? null : document.Title.Trim();
    }

    private static string StripChapterNumberPrefix(string title)
    {
        var match = ChapterPrefixRegex().Match(title.Trim());
        return match.Success ? match.Groups[1].Value.Trim() : title.Trim();
    }

    private static (byte[]? Bytes, string? FileName) ExtractCoverImage(ZipArchive archive, EpubPackage package)
    {
        var coverItem = package.ManifestById.Values.FirstOrDefault(i => i.IsCoverImage);
        if (coverItem is null)
            return (null, null);

        var coverPath = CombineEpubPath(package.OpfDirectory, coverItem.Href);
        var coverEntry = archive.GetEntry(coverPath);
        if (coverEntry is null)
            return (null, null);

        using var coverStream = coverEntry.Open();
        using var memoryStream = new MemoryStream();
        coverStream.CopyTo(memoryStream);
        return (memoryStream.ToArray(), Path.GetFileName(coverItem.Href));
    }

    private static string UniqueFileName(string desiredFileName, IEnumerable<string> existingFileNames)
    {
        var existing = new HashSet<string>(existingFileNames, StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(desiredFileName))
            return desiredFileName;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(desiredFileName);
        var ext = Path.GetExtension(desiredFileName);
        var index = 2;
        string candidate;
        do
        {
            candidate = $"{nameWithoutExt}-{index}{ext}";
            index++;
        } while (existing.Contains(candidate));

        return candidate;
    }

    private static string StripFragment(string href) => href.Split('#')[0];

    /// <summary>Resolves a manifest href relative to a base directory, collapsing "./"/"../"
    /// segments — EPUB manifest hrefs are relative to the OPF's own directory (or, for an
    /// image referenced from a chapter body, relative to that chapter's own directory), never
    /// a hardcoded prefix.</summary>
    private static string CombineEpubPath(string baseDir, string relativeHref)
    {
        var combined = string.IsNullOrEmpty(baseDir) ? relativeHref : $"{baseDir}/{relativeHref}";
        var segments = new List<string>();
        foreach (var segment in combined.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        return string.Join('/', segments);
    }
}

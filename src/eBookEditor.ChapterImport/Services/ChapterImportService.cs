using eBookEditor.ChapterImport.Models;
using eBookEditor.Core.Services;
using eBookEditor.DocxImport.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.ChapterImport.Services;

/// <summary>
/// Converts an arbitrary dropped/picked file into one or more chapter drafts, ready to be
/// written to disk and inserted into a project's spine. Handles .ebhtml (this app's own
/// native chapter format — used as-is; also how OrphanChapterScanner-found files, already in
/// this project's own chapters/ directory, come back through here), .md (legacy, also used
/// as-is for now — see the HTML content-model refactor's migration tooling for real .md
/// project upgrades), .docx (reuses the whole-manuscript importer's chapter-boundary
/// detection, emitting HTML), and .html/.htm (sanitized pass-through via HtmlImportSanitizer).
/// The source file's name supplies both the chapter title and an optional position hint (see
/// ChapterFileNaming.ParseHint), e.g. "23. What Now.md" -> chapter 23, titled "What Now".
/// </summary>
public class ChapterImportService
{
    private readonly DocxImportService _docxImportService = new();
    private readonly HtmlImportSanitizer _htmlSanitizer = new();
    private readonly ChapterFileService _chapterFileService = new();

    public IReadOnlyList<ChapterImportDraft> ImportFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var (hintNumber, hintTitle) = ChapterFileNaming.ParseHint(Path.GetFileNameWithoutExtension(filePath));

        return extension switch
        {
            ".ebhtml" or ".md" => [ImportNativeChapterFile(filePath, hintNumber, hintTitle)],
            ".docx" => ImportDocxFile(filePath, hintNumber, hintTitle),
            ".html" or ".htm" => [ImportHtmlFile(filePath, hintNumber, hintTitle)],
            _ => throw new NotSupportedException($"Unsupported chapter file type: '{extension}'. Expected .ebhtml, .md, .docx, .html, or .htm.")
        };
    }

    private ChapterImportDraft ImportNativeChapterFile(string filePath, int? hintNumber, string hintTitle)
    {
        var (frontMatter, body) = _chapterFileService.ParseChapter(File.ReadAllText(filePath));
        var title = string.IsNullOrWhiteSpace(frontMatter.Title) ? hintTitle : frontMatter.Title!;
        var (type, numberMode) = SpecialPageClassifier.Classify(title);
        return new ChapterImportDraft(title, body.Trim(), hintNumber, [], type, numberMode);
    }

    private ChapterImportDraft ImportHtmlFile(string filePath, int? hintNumber, string hintTitle)
    {
        var html = SameDocumentLinkConverter.Convert(_htmlSanitizer.Convert(File.ReadAllText(filePath)));
        var (type, numberMode) = SpecialPageClassifier.Classify(hintTitle);
        return new ChapterImportDraft(hintTitle, html, hintNumber, [], type, numberMode);
    }

    private IReadOnlyList<ChapterImportDraft> ImportDocxFile(string filePath, int? hintNumber, string hintTitle)
    {
        var drafts = _docxImportService.Import(filePath);

        // A dropped .docx with no internal chapter-heading structure comes back as a single
        // draft; the file name's position hint applies to it, and since such a file typically
        // has no internal heading for SpecialPageClassifier to have already looked at (see
        // DocxImportService), classify off the file name's own title instead — so dropping a
        // file literally named "Preface.docx" still lands as a front-matter page. A .docx that
        // DID auto-split into multiple chapters (same heading detection as the whole-manuscript
        // import) keeps its own internal order and per-heading classification instead — a
        // single hint (or the file name's title) wouldn't make sense across several results.
        //
        // SameDocumentLinkConverter runs per-draft (see OpenXmlToHtmlConverter's own doc
        // comments on how a Word bookmark/internal hyperlink reach this point as raw "id"/
        // "#name" markup) — a bookmark and the hyperlink referencing it that land in different
        // drafts (chapters) simply won't resolve, an accepted "best effort, same-chapter-only"
        // simplification rather than the harder cross-chapter resolution EPUB import performs
        // (see EpubInternalHrefRewriter).
        if (drafts.Count == 1)
        {
            var (type, numberMode) = SpecialPageClassifier.Classify(hintTitle);
            return [new ChapterImportDraft(drafts[0].Title, SameDocumentLinkConverter.Convert(drafts[0].Body), hintNumber, drafts[0].Images, type, numberMode)];
        }

        return drafts.Select(d => new ChapterImportDraft(d.Title, SameDocumentLinkConverter.Convert(d.Body), null, d.Images, d.Type, d.NumberMode)).ToList();
    }
}

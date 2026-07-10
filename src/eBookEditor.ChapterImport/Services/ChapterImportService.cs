using eBookEditor.ChapterImport.Models;
using eBookEditor.Core.Services;
using eBookEditor.DocxImport.Services;

namespace eBookEditor.ChapterImport.Services;

/// <summary>
/// Converts an arbitrary dropped/picked file into one or more chapter drafts, ready to be
/// written to disk and inserted into a project's spine. Handles .ebhtml (this app's own
/// native chapter format — used as-is; also how OrphanChapterScanner-found files, already in
/// this project's own chapters/ directory, come back through here), .md (legacy, also used
/// as-is for now — see the HTML content-model refactor's migration tooling for real .md
/// project upgrades), .docx (reuses the whole-manuscript importer's chapter-boundary
/// detection), and .html/.htm (converted via HtmlToMarkdownConverter). The source file's name
/// supplies both the chapter title and an optional position hint (see
/// ChapterFileNaming.ParseHint), e.g. "23. What Now.md" -> chapter 23, titled "What Now".
/// </summary>
public class ChapterImportService
{
    private readonly DocxImportService _docxImportService = new();
    private readonly HtmlToMarkdownConverter _htmlConverter = new();
    private readonly ChapterFileService _chapterFileService = new();

    public IReadOnlyList<ChapterImportDraft> ImportFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var (hintNumber, hintTitle) = ChapterFileNaming.ParseHint(Path.GetFileNameWithoutExtension(filePath));

        return extension switch
        {
            ".ebhtml" or ".md" => [ImportNativeChapterFile(filePath, hintNumber, hintTitle)],
            ".docx" => ImportDocxFile(filePath, hintNumber),
            ".html" or ".htm" => [ImportHtmlFile(filePath, hintNumber, hintTitle)],
            _ => throw new NotSupportedException($"Unsupported chapter file type: '{extension}'. Expected .ebhtml, .md, .docx, .html, or .htm.")
        };
    }

    private ChapterImportDraft ImportNativeChapterFile(string filePath, int? hintNumber, string hintTitle)
    {
        var (frontMatter, body) = _chapterFileService.ParseChapter(File.ReadAllText(filePath));
        var title = string.IsNullOrWhiteSpace(frontMatter.Title) ? hintTitle : frontMatter.Title!;
        return new ChapterImportDraft(title, body.Trim(), hintNumber, []);
    }

    private ChapterImportDraft ImportHtmlFile(string filePath, int? hintNumber, string hintTitle)
    {
        var markdown = _htmlConverter.Convert(File.ReadAllText(filePath));
        return new ChapterImportDraft(hintTitle, markdown, hintNumber, []);
    }

    private IReadOnlyList<ChapterImportDraft> ImportDocxFile(string filePath, int? hintNumber)
    {
        var drafts = _docxImportService.Import(filePath);

        // A dropped .docx with no internal chapter-heading structure comes back as a single
        // draft; the file name's position hint applies to it. A .docx that DID auto-split
        // into multiple chapters (same heading detection as the whole-manuscript import)
        // keeps its own internal order instead — a single hint wouldn't make sense across
        // several resulting chapters.
        if (drafts.Count == 1)
            return [new ChapterImportDraft(drafts[0].Title, drafts[0].BodyMarkdown, hintNumber, drafts[0].Images)];

        return drafts.Select(d => new ChapterImportDraft(d.Title, d.BodyMarkdown, null, d.Images)).ToList();
    }
}

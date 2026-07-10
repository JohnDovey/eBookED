using System.Text;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>
/// Concatenates every spine item's stored HTML body into one whole-book HTML fragment, in
/// spine order, for the whole-book Word export (HtmlToDocxConverter.ConvertToFile takes a
/// single HTML string for the whole document, unlike PdfBuilder, which calls the PDF renderer
/// once per spine item within one QuestPDF document). Each chapter's heading is synthesized via
/// ChapterHeadingHtml (front/back matter pages already carry their own, baked in by
/// PageGeneratorService). Sections are separated with an "hr" thematic break, matching the
/// page-break-per-section convention already used by the EPUB and PDF exports.
/// </summary>
public class HtmlBookAssembler
{
    private readonly ChapterFileService _chapterFileService = new();

    public string AssembleWholeBook(EbookProject project)
    {
        var sb = new StringBuilder();
        var ordered = project.Spine.OrderBy(i => i.Order).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            sb.Append(RenderSection(project, ordered[i]));

            if (i < ordered.Count - 1)
                sb.AppendLine("\n<hr>\n");
        }

        return sb.ToString();
    }

    private string RenderSection(EbookProject project, SpineItem item)
    {
        var path = project.ResolvePath(item);
        var rawText = File.ReadAllText(path);
        var (_, body) = _chapterFileService.ParseChapter(rawText);

        if (ChapterHeadingHtml.Build(item) is not { } heading)
            return body.TrimEnd() + "\n";

        var sb = new StringBuilder();
        sb.AppendLine(heading);
        sb.AppendLine(body.TrimEnd());
        return sb.ToString();
    }
}

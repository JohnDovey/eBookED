using System.Net;
using System.Text;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>
/// Concatenates every spine item's stored HTML body into one whole-book HTML fragment, in
/// spine order, for the whole-book Word export (HtmlToDocxConverter.ConvertToFile takes a
/// single HTML string for the whole document, unlike PdfBuilder, which calls the PDF renderer
/// once per spine item within one QuestPDF document). Front/back matter pages already carry
/// their own heading (from PageGeneratorService), so only chapters get a synthesized one —
/// chapter files store the title only in YAML front matter, not in the body. Sections are
/// separated with an "hr" thematic break, matching the page-break-per-section convention
/// already used by the EPUB and PDF exports.
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

        if (item.Type != SpineItemType.Chapter)
            return body.TrimEnd() + "\n";

        var heading = item.ResolvedNumber is { } number
            ? $"<h1>Chapter {number}: {WebUtility.HtmlEncode(item.Title)}</h1>"
            : $"<h1>{WebUtility.HtmlEncode(item.Title)}</h1>";

        var sb = new StringBuilder();
        sb.AppendLine(heading);

        if (!string.IsNullOrWhiteSpace(item.Subtitle))
            sb.AppendLine($"<h2>{WebUtility.HtmlEncode(item.Subtitle)}</h2>");

        sb.AppendLine(body.TrimEnd());
        return sb.ToString();
    }
}

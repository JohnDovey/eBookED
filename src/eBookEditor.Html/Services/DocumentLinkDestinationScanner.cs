using AngleSharp.Html.Parser;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>A destination "Mark Link Destination" created somewhere in the book — Label is the
/// marked text as it currently reads in that chapter's body (not whatever the user originally
/// typed into the mark-destination dialog, which can drift out of sync with later edits).</summary>
public sealed record LinkDestination(SpineItem Item, string DestinationId, string Label);

/// <summary>
/// Scans every spine item's stored body for "Mark Link Destination" markers (see
/// InternalLinkConvention) — the first service in this codebase to scan every chapter/page's
/// actual body rather than just spine metadata (PageGeneratorService.GenerateTocPage, for
/// comparison, only ever reads SpineItem.Title). Powers "Insert Internal Link"'s chapter/
/// destination picker and its "no destinations exist yet" empty-state message.
/// </summary>
public class DocumentLinkDestinationScanner
{
    private static readonly HtmlParser Parser = new();
    private readonly ChapterFileService _chapterFileService = new();

    public IReadOnlyList<LinkDestination> FindAll(EbookProject project)
    {
        var results = new List<LinkDestination>();

        foreach (var item in project.Spine.OrderBy(i => i.Order))
        {
            var path = project.ResolvePath(item);
            if (!File.Exists(path))
                continue;

            var (_, body) = _chapterFileService.ReadChapter(path);
            var document = Parser.ParseDocument($"<body>{body}</body>");

            foreach (var element in document.QuerySelectorAll($"[id^='{InternalLinkConvention.DestinationIdPrefix}']"))
                results.Add(new LinkDestination(item, element.Id!, element.TextContent.Trim()));
        }

        return results;
    }
}

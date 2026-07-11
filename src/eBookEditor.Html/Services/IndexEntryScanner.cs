using AngleSharp.Html.Parser;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>One "Mark as Index Entry" occurrence — Term is the marker's own data-index-term
/// attribute value, not re-derived from the marked text, so a user can mark differently-cased or
/// differently-punctuated occurrences ("Captain" / "the captain's") as the same index term.</summary>
public sealed record IndexOccurrence(SpineItem Item, string Term, string MarkerId);

/// <summary>
/// Scans every spine item's stored body for "Mark as Index Entry" markers (see
/// InternalLinkConvention) — parallel to DocumentLinkDestinationScanner, this app's other
/// whole-book body scanner. Powers "Generate/Regenerate Index" (see
/// PageGeneratorService.GenerateIndexPage, which groups/alphabetizes this scanner's raw
/// occurrence list by term).
/// </summary>
public class IndexEntryScanner
{
    private static readonly HtmlParser Parser = new();
    private readonly ChapterFileService _chapterFileService = new();

    public IReadOnlyList<IndexOccurrence> FindAll(EbookProject project)
    {
        var results = new List<IndexOccurrence>();

        foreach (var item in project.Spine.OrderBy(i => i.Order))
        {
            var path = project.ResolvePath(item);
            if (!File.Exists(path))
                continue;

            var (_, body) = _chapterFileService.ReadChapter(path);
            var document = Parser.ParseDocument($"<body>{body}</body>");

            foreach (var element in document.QuerySelectorAll($"[id^='{InternalLinkConvention.IndexEntryIdPrefix}']"))
            {
                var term = element.GetAttribute(InternalLinkConvention.IndexTermDataAttribute);
                if (term is { Length: > 0 })
                    results.Add(new IndexOccurrence(item, term, element.Id!));
            }
        }

        return results;
    }
}

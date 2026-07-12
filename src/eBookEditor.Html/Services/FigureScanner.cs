using AngleSharp.Html.Parser;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>One inserted image's &lt;figure&gt; — Caption is read straight from the figure's own
/// &lt;figcaption&gt; text, not tracked separately, so it always reflects whatever the user last
/// typed there (including a direct edit made outside "Edit Image…").</summary>
public sealed record FigureOccurrence(SpineItem Item, string FigureId, string Caption);

/// <summary>
/// Scans every spine item's stored body for &lt;figure&gt; elements — parallel to
/// IndexEntryScanner, this app's other whole-book body scanner. Powers "Generate/Regenerate List
/// of Figures" (see PageGeneratorService.GenerateListOfFiguresPage). Unlike IndexEntryScanner,
/// this one can mutate: "Insert Image…" (see MainWindow.OnInsertImageClick) gives every new
/// figure an InternalLinkConvention.FigureIdPrefix id up front, but older content — imported, or
/// inserted before this feature existed — may have figures with no id at all, so this backfills
/// one and persists the chapter file the first time it's scanned, the same "seed it on first use"
/// spirit as MainWindowViewModel.GenerateIndex auto-adding the back-matter spine item. Only
/// figures with a non-empty caption are listed — an uncaptioned decorative image has nothing
/// meaningful to show in a List of Figures entry.
/// </summary>
public class FigureScanner
{
    private static readonly HtmlParser Parser = new();
    private readonly ChapterFileService _chapterFileService = new();

    public IReadOnlyList<FigureOccurrence> FindAll(EbookProject project)
    {
        var results = new List<FigureOccurrence>();

        foreach (var item in project.Spine.OrderBy(i => i.Order))
        {
            var path = project.ResolvePath(item);
            if (!File.Exists(path))
                continue;

            var (frontMatter, body) = _chapterFileService.ReadChapter(path);
            var document = Parser.ParseDocument($"<body>{body}</body>");
            var figures = document.QuerySelectorAll("figure").ToList();
            if (figures.Count == 0)
                continue;

            var mutated = false;
            foreach (var figure in figures)
            {
                if (string.IsNullOrEmpty(figure.Id) || !figure.Id.StartsWith(InternalLinkConvention.FigureIdPrefix, StringComparison.Ordinal))
                {
                    figure.Id = $"{InternalLinkConvention.FigureIdPrefix}{Guid.NewGuid():N}";
                    mutated = true;
                }

                var caption = figure.QuerySelector("figcaption")?.TextContent.Trim() ?? "";
                if (caption.Length > 0)
                    results.Add(new FigureOccurrence(item, figure.Id!, caption));
            }

            if (mutated)
                _chapterFileService.WriteChapter(path, frontMatter, document.Body!.InnerHtml);
        }

        return results;
    }
}

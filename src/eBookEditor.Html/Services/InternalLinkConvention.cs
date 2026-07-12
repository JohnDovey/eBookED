namespace eBookEditor.Html.Services;

/// <summary>
/// This app's internal-link conventions, both reserved id prefixes never colliding with
/// footnotes' "fn:"/"fnref:" or a user's own hand-typed heading id (Slug.Create-based ids this
/// app generates elsewhere never contain a colon):
///
/// "Mark Link Destination"/"Insert Internal Link" (see
/// MainWindow.OnMarkLinkDestinationClick/OnInsertInternalLinkClick) establishes: a destination is
/// &lt;span id="dest:{slug}"&gt;{marked text}&lt;/span&gt;, and a link to it is
/// &lt;a href="{SpineItem.RelativePath}#dest:{slug}"&gt; — the same RelativePath-based chapter
/// targeting EpubInternalLinkResolver already uses, with a fragment identifying a specific
/// destination within that chapter.
///
/// "Mark as Index Entry" (see MainWindow.OnMarkIndexEntryClick) establishes: an occurrence is
/// &lt;span class="index-entry" data-index-term="{term}" id="idx:{slug}"&gt;{marked text}&lt;/span&gt;
/// — see IndexEntryScanner/IndexEntryMarker and PageGeneratorService.GenerateIndexPage, which
/// link back to each occurrence the same way a "dest:" link does.
/// </summary>
public static class InternalLinkConvention
{
    public const string DestinationIdPrefix = "dest:";
    public const string IndexEntryIdPrefix = "idx:";
    public const string IndexEntryClass = "index-entry";
    public const string IndexTermDataAttribute = "data-index-term";

    /// <summary>Every &lt;figure&gt; inserted via "Insert Image…" gets one of these ids (see
    /// MainWindow.OnInsertImageClick) so the List of Figures/Photos page can link back to it —
    /// same role as "dest:"/"idx:" above, see FigureScanner/PageGeneratorService.GenerateListOfFiguresPage.</summary>
    public const string FigureIdPrefix = "fig:";

    public static bool IsInternalMarkerId(string? id) =>
        id is { Length: > 0 } && (id.StartsWith(DestinationIdPrefix, StringComparison.Ordinal)
            || id.StartsWith(IndexEntryIdPrefix, StringComparison.Ordinal)
            || id.StartsWith(FigureIdPrefix, StringComparison.Ordinal));

    /// <summary>True if <paramref name="href"/> is an internal link to a "dest:"/"idx:"/"fig:"
    /// marker (same-chapter or cross-chapter), with <paramref name="destinationId"/> set to the
    /// full fragment (prefix included) — the same string used as both the marker's own id and,
    /// as-is, this app's export-time PDF Section/Word bookmark name.</summary>
    public static bool TryGetDestinationFragment(string? href, out string destinationId)
    {
        if (href is { Length: > 0 })
        {
            var hashIndex = href.IndexOf('#');
            if (hashIndex >= 0)
            {
                var fragment = href[(hashIndex + 1)..];
                if (IsInternalMarkerId(fragment))
                {
                    destinationId = fragment;
                    return true;
                }
            }
        }

        destinationId = "";
        return false;
    }
}

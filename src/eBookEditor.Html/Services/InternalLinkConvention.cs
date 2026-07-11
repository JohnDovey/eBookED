namespace eBookEditor.Html.Services;

/// <summary>
/// The cross-document link convention "Mark Link Destination"/"Insert Internal Link" (see
/// MainWindow.OnMarkLinkDestinationClick/OnInsertInternalLinkClick) establishes: a destination is
/// &lt;span id="dest:{slug}"&gt;{marked text}&lt;/span&gt;, and a link to it is
/// &lt;a href="{SpineItem.RelativePath}#dest:{slug}"&gt; — the same RelativePath-based chapter
/// targeting EpubInternalLinkResolver already uses, with a fragment identifying a specific
/// destination within that chapter. "dest:" is reserved and never collides with footnotes'
/// "fn:"/"fnref:" or a user's own hand-typed heading id, since Slug.Create-based ids this app
/// generates elsewhere never contain a colon.
/// </summary>
public static class InternalLinkConvention
{
    public const string DestinationIdPrefix = "dest:";

    /// <summary>True if <paramref name="href"/> is an internal link to a destination marker
    /// (same-chapter or cross-chapter), with <paramref name="destinationId"/> set to the full
    /// "dest:{slug}" fragment — the same string used as both the destination span's own id and,
    /// with a leading "dest:", as this app's export-time PDF Section/Word bookmark name.</summary>
    public static bool TryGetDestinationFragment(string? href, out string destinationId)
    {
        if (href is { Length: > 0 })
        {
            var hashIndex = href.IndexOf('#');
            if (hashIndex >= 0)
            {
                var fragment = href[(hashIndex + 1)..];
                if (fragment.StartsWith(DestinationIdPrefix, StringComparison.Ordinal))
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

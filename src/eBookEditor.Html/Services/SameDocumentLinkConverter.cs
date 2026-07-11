using System.Net;
using System.Text.RegularExpressions;

namespace eBookEditor.Html.Services;

/// <summary>
/// Converts a same-document "#id" anchor link (and its target element's id) into this app's own
/// "dest:" cross-document link convention (see InternalLinkConvention) — used by DOCX and HTML
/// import for a same-page reference that already works natively in a real browser (Preview/
/// WYSIWYG resolve a plain "#id" fragment fine on their own) but wouldn't otherwise be recognized
/// as a real jump target by PDF/Word export, which only look for the "dest:" prefix specifically.
/// Only converts a fragment whose target element can actually be found in the same html — a
/// fragment pointing nowhere real is left exactly as it was, not converted into a dest: link to
/// nowhere. Scoped to same-document links only (no cross-chapter resolution, unlike EPUB import's
/// own EpubInternalHrefRewriter) since both DOCX and HTML import already hand this converter one
/// already-finalized chapter body at a time, with no visibility into any other chapter.
/// </summary>
public static partial class SameDocumentLinkConverter
{
    [GeneratedRegex("""<a\s+([^>]*?)href\s*=\s*"#([^"]+)"([^>]*)>""", RegexOptions.IgnoreCase)]
    private static partial Regex SameDocumentHrefRegex();

    [GeneratedRegex("""\sid\s*=\s*"([^"]*)"|\sid\s*=\s*'([^']*)'""", RegexOptions.IgnoreCase)]
    private static partial Regex IdAttributeRegex();

    /// <summary>Converts every resolvable same-document "#id" link in <paramref name="html"/>
    /// into a "dest:" link, leaving anything unresolvable (no matching id) or reserved (a
    /// footnote's own "fn:"/"fnref:" fragment) untouched.</summary>
    public static string Convert(string html)
    {
        var fragments = SameDocumentHrefRegex().Matches(html)
            .Select(m => WebUtility.HtmlDecode(m.Groups[2].Value))
            .Where(fragment => !fragment.StartsWith("fn:", StringComparison.Ordinal) && !fragment.StartsWith("fnref:", StringComparison.Ordinal))
            .Distinct()
            .Where(fragment => HasId(html, fragment))
            .ToList();

        if (fragments.Count == 0)
            return html;

        var destIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fragment in fragments)
        {
            var baseSlug = eBookEditor.Core.Services.Slug.Create(fragment, "target");
            var slug = baseSlug;
            var suffix = 2;
            while (!usedSlugs.Add(slug))
                slug = $"{baseSlug}-{suffix++}";

            destIds[fragment] = $"{InternalLinkConvention.DestinationIdPrefix}{slug}";
        }

        var renamed = RenameIds(html, destIds);
        return RewriteHrefs(renamed, destIds);
    }

    private static bool HasId(string html, string id) =>
        IdAttributeRegex().Matches(html).Any(m => WebUtility.HtmlDecode(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) == id);

    private static string RenameIds(string html, IReadOnlyDictionary<string, string> originalIdToDestId) =>
        IdAttributeRegex().Replace(html, match =>
        {
            var originalId = WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
            return originalIdToDestId.TryGetValue(originalId, out var destId)
                ? $" id=\"{WebUtility.HtmlEncode(destId)}\""
                : match.Value;
        });

    private static string RewriteHrefs(string html, IReadOnlyDictionary<string, string> fragmentToDestId) =>
        SameDocumentHrefRegex().Replace(html, match =>
        {
            var fragment = WebUtility.HtmlDecode(match.Groups[2].Value);
            if (!fragmentToDestId.TryGetValue(fragment, out var destId))
                return match.Value;

            var before = match.Groups[1].Value;
            var after = match.Groups[3].Value;
            return $"<a {before}href=\"#{WebUtility.HtmlEncode(destId)}\"{after}>";
        });
}

using System.Net;
using System.Text.RegularExpressions;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Rewrites internal cross-chapter <c>&lt;a href="..."&gt;</c> links found in an imported
/// chapter's body so they point at the corresponding item's new project-relative path (a
/// SpineItem.RelativePath value, e.g. "chapters/003-Foo.ebhtml" — the same format
/// EpubInternalLinkResolver looks up on export) instead of the source EPUB's original
/// filename. A new, purpose-built rewriter rather than reusing EpubInternalLinkResolver
/// (export direction): that resolver expects an exact, fragment-free key match, which real
/// imported hrefs often aren't (a link into a specific in-chapter section, e.g.
/// "chapter3.xhtml#section2", is common).
///
/// <see cref="Rewrite"/> alone drops any fragment, preserving only the chapter-level target —
/// EpubProjectImporter now only falls back to that for a fragment it can't confirm resolves to
/// a real element (see <see cref="FindFragmentReferences"/>/<see cref="HasId"/>). Where the
/// fragment's target element is actually found, EpubProjectImporter instead converts it into
/// this app's own "dest:" cross-document link convention (see InternalLinkConvention in
/// eBookEditor.Html) via <see cref="RenameIds"/> (retargeting the element's own id) and
/// <see cref="RewriteHrefsWithFragments"/> (pointing the link at it) — a real, working
/// in-document jump instead of the previously silently-dropped fragment.
/// </summary>
public static partial class EpubInternalHrefRewriter
{
    [GeneratedRegex("""<a\s+([^>]*?)href\s*=\s*"([^"]*)"([^>]*)>""", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorHrefRegex();

    [GeneratedRegex("""\sid\s*=\s*"([^"]*)"|\sid\s*=\s*'([^']*)'""", RegexOptions.IgnoreCase)]
    private static partial Regex IdAttributeRegex();

    public static string Rewrite(string html, IReadOnlyDictionary<string, string> sourceHrefToRelativePath)
    {
        if (sourceHrefToRelativePath.Count == 0)
            return html;

        return AnchorHrefRegex().Replace(html, match =>
        {
            var rawHref = WebUtility.HtmlDecode(match.Groups[2].Value);
            var baseHref = rawHref.Split('#')[0];

            if (!sourceHrefToRelativePath.TryGetValue(baseHref, out var relativePath))
                return match.Value;

            var before = match.Groups[1].Value;
            var after = match.Groups[3].Value;
            return $"<a {before}href=\"{WebUtility.HtmlEncode(relativePath)}\"{after}>";
        });
    }

    /// <summary>Every (TargetHref, Fragment) pair referenced by an anchor with a non-empty
    /// fragment in <paramref name="html"/> — TargetHref is "" for a same-chapter reference
    /// (e.g. "#section2"), the same convention <see cref="RewriteHrefsWithFragments"/>'s
    /// <c>currentChapterSourceHref</c> substitution expects.</summary>
    public static IReadOnlyList<(string TargetHref, string Fragment)> FindFragmentReferences(string html) =>
        AnchorHrefRegex().Matches(html)
            .Select(m => WebUtility.HtmlDecode(m.Groups[2].Value))
            .Select(href => href.IndexOf('#') is var hashIndex && hashIndex >= 0
                ? (TargetHref: href[..hashIndex], Fragment: href[(hashIndex + 1)..])
                : (TargetHref: href, Fragment: ""))
            .Where(pair => pair.Fragment.Length > 0)
            .Distinct()
            .ToList();

    /// <summary>True if some element in <paramref name="html"/> carries the exact id
    /// <paramref name="id"/> — used to confirm a fragment reference's target really exists
    /// before converting it into a dest: link, rather than manufacturing a link to nowhere.</summary>
    public static bool HasId(string html, string id) =>
        IdAttributeRegex().Matches(html).Any(m => WebUtility.HtmlDecode(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) == id);

    /// <summary>Retargets every element whose id is a key in <paramref name="originalIdToDestId"/>
    /// to that dest: id instead — the "mark the destination" half of converting a resolved
    /// fragment reference (see the class doc comment).</summary>
    public static string RenameIds(string html, IReadOnlyDictionary<string, string> originalIdToDestId)
    {
        if (originalIdToDestId.Count == 0)
            return html;

        return IdAttributeRegex().Replace(html, match =>
        {
            var originalId = WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
            return originalIdToDestId.TryGetValue(originalId, out var destId)
                ? $" id=\"{WebUtility.HtmlEncode(destId)}\""
                : match.Value;
        });
    }

    /// <summary>
    /// The fragment-aware sibling of <see cref="Rewrite"/>: an anchor whose (resolved)
    /// TargetHref/Fragment pair has an entry in <paramref name="fragmentDestIds"/> is rewritten
    /// to "{relativePath}#{destId}" (a real, working in-document jump); anything else — no
    /// fragment, an unresolvable target chapter, or a fragment whose target element was never
    /// found (no entry in <paramref name="fragmentDestIds"/>) — falls back to the same
    /// chapter-level-only rewrite <see cref="Rewrite"/> performs. <paramref
    /// name="currentChapterSourceHref"/> stands in for a same-chapter "#fragment" reference's
    /// empty TargetHref, so it must itself be a key in <paramref
    /// name="sourceHrefToRelativePath"/> for a same-chapter link to resolve.
    /// </summary>
    public static string RewriteHrefsWithFragments(
        string html,
        string currentChapterSourceHref,
        IReadOnlyDictionary<string, string> sourceHrefToRelativePath,
        IReadOnlyDictionary<(string TargetHref, string Fragment), string> fragmentDestIds)
    {
        if (sourceHrefToRelativePath.Count == 0)
            return html;

        return AnchorHrefRegex().Replace(html, match =>
        {
            var rawHref = WebUtility.HtmlDecode(match.Groups[2].Value);
            var hashIndex = rawHref.IndexOf('#');
            var baseHref = hashIndex >= 0 ? rawHref[..hashIndex] : rawHref;
            var fragment = hashIndex >= 0 ? rawHref[(hashIndex + 1)..] : "";
            var effectiveTargetHref = baseHref.Length == 0 ? currentChapterSourceHref : baseHref;

            if (!sourceHrefToRelativePath.TryGetValue(effectiveTargetHref, out var relativePath))
                return match.Value;

            var before = match.Groups[1].Value;
            var after = match.Groups[3].Value;

            var newHref = fragment.Length > 0 && fragmentDestIds.TryGetValue((effectiveTargetHref, fragment), out var destId)
                ? $"{relativePath}#{destId}"
                : relativePath;

            return $"<a {before}href=\"{WebUtility.HtmlEncode(newHref)}\"{after}>";
        });
    }
}

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
/// "chapter3.xhtml#section2", is common). Any fragment is dropped when rewriting — the
/// chapter-level target is preserved, a link into a specific section within it is not (this
/// app has no equivalent in-chapter anchor convention to preserve it against) — a documented
/// limitation, not a bug.
/// </summary>
public static partial class EpubInternalHrefRewriter
{
    [GeneratedRegex("""<a\s+([^>]*?)href\s*=\s*"([^"]*)"([^>]*)>""", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorHrefRegex();

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
}

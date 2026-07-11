using System.Net;
using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Rewrites HTML links that point at another spine item's project-relative source path
/// (e.g. "chapters/foo-abc123.ebhtml", as written by PageGeneratorService.GenerateTocPage) to
/// that item's assigned EPUB content document filename, so the in-book table of contents
/// page is actually clickable in the exported EPUB instead of linking at a path that only
/// exists in the source project directory, not the packaged EPUB. A cross-chapter link into a
/// specific in-page marker (this app's own "dest:"/"idx:" convention — see
/// InternalLinkConvention) keeps its "#fragment" — only the base path before it is looked up
/// and rewritten.
/// </summary>
internal static partial class EpubInternalLinkResolver
{
    [GeneratedRegex(@"<a\s+href=""([^""]+)""([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    public static string RewriteChapterLinks(string html, IReadOnlyDictionary<string, string> contentFileNamesByRelativePath)
    {
        return LinkRegex().Replace(html, match =>
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            var rest = match.Groups[2].Value;

            var hashIndex = href.IndexOf('#');
            var basePath = hashIndex >= 0 ? href[..hashIndex] : href;
            var fragment = hashIndex >= 0 ? href[hashIndex..] : "";

            return contentFileNamesByRelativePath.TryGetValue(basePath, out var fileName)
                ? $"<a href=\"{fileName}{fragment}\"{rest}>"
                : match.Value;
        });
    }
}

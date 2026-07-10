using System.Net;
using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Rewrites HTML links that point at another spine item's project-relative source path
/// (e.g. "chapters/foo-abc123.ebhtml", as written by PageGeneratorService.GenerateTocPage) to
/// that item's assigned EPUB content document filename, so the in-book table of contents
/// page is actually clickable in the exported EPUB instead of linking at a path that only
/// exists in the source project directory, not the packaged EPUB.
/// </summary>
internal static partial class EpubInternalLinkResolver
{
    [GeneratedRegex(@"<a\s+href=""([^""]+)""([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    public static string RewriteChapterLinks(string html, IReadOnlyDictionary<string, string> contentFileNamesByRelativePath)
    {
        return LinkRegex().Replace(html, match =>
        {
            var path = WebUtility.HtmlDecode(match.Groups[1].Value);
            var rest = match.Groups[2].Value;

            return contentFileNamesByRelativePath.TryGetValue(path, out var fileName)
                ? $"<a href=\"{fileName}\"{rest}>"
                : match.Value;
        });
    }
}

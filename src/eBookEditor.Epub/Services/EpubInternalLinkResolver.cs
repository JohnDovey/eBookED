using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Rewrites markdown links that point at another spine item's project-relative source path
/// (e.g. "chapters/foo-abc123.md", as written by PageGeneratorService.GenerateTocPage) to
/// that item's assigned EPUB content document filename, so the in-book table of contents
/// page is actually clickable in the exported EPUB instead of linking at a path that only
/// exists in the source project directory, not the packaged EPUB.
/// </summary>
internal static partial class EpubInternalLinkResolver
{
    // A link destination is either angle-bracket-wrapped (allows spaces, as GenerateTocPage
    // emits for chapter paths like "chapters/001 - Getting Ready.md") or bare (no spaces
    // allowed, per CommonMark) — group 2 or group 3 respectively.
    [GeneratedRegex("""(?<!!)\[([^\]]*)\]\((?:<([^>]+)>|([^)\s]+))(?:\s+"[^"]*")?\)""")]
    private static partial Regex LinkRegex();

    public static string RewriteChapterLinks(string markdown, IReadOnlyDictionary<string, string> contentFileNamesByRelativePath)
    {
        return LinkRegex().Replace(markdown, match =>
        {
            var text = match.Groups[1].Value;
            var path = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

            return contentFileNamesByRelativePath.TryGetValue(path, out var fileName)
                ? $"[{text}]({fileName})"
                : match.Value;
        });
    }
}

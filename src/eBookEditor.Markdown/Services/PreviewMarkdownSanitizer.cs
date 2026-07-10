using System.Text.RegularExpressions;

namespace eBookEditor.Markdown.Services;

/// <summary>
/// Markdown.Avalonia (the Open Preview window's rendering engine) is a separate, independent
/// Markdown parser — not Markdig — with no support at all for Pandoc-style custom containers
/// (":::") or PHP Markdown Extra's special attribute blocks ("{#id .class}"). Feeding it either
/// verbatim doesn't just leave them unstyled (as PDF/Word already document for CSS classes):
/// custom-container fences render as a broken-looking literal code block showing the raw ":::"
/// syntax, and attribute blocks show up as ugly literal "{...}" text glued onto headings or
/// sitting on their own line after a table/definition list. Since Preview has no CSS engine of
/// its own either way (Markdown.Avalonia styles via Avalonia XAML, not the project's actual CSS
/// template), there's no way to make these constructs look *right* there — stripping them at
/// least makes Preview look *clean*, matching what a reader would see content-wise if not
/// styling-wise. This never touches the saved file; it only transforms what's fed to the
/// preview's own renderer.
/// </summary>
public static partial class PreviewMarkdownSanitizer
{
    // A bare custom-container fence line: "::: {.class}" (open) or ":::" (close), any run of
    // 3+ colons.
    [GeneratedRegex(@"^:{3,}[^\n]*$", RegexOptions.Multiline)]
    private static partial Regex ContainerFenceLine();

    // An attribute block at the end of a line — either glued onto real content ("# Heading
    // {#id .class}", "[text](url){.class}") or standalone on its own line (the "{.caption}"
    // PHP Markdown Extra allows after a table or definition list; matches here too since
    // nothing is required before the brace). Restricted to token shapes that actually look
    // like id/class/key=value attributes, so it doesn't eat an unrelated trailing "{...}"
    // (e.g. inline code ending in braces).
    [GeneratedRegex(@"[ \t]*\{(?:[#.][\w-]+|[\w-]+=\S+)(?:\s+(?:[#.][\w-]+|[\w-]+=\S+))*\}[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex TrailingAttributeBlock();

    public static string Sanitize(string markdown)
    {
        var withoutFences = ContainerFenceLine().Replace(markdown, string.Empty);
        return TrailingAttributeBlock().Replace(withoutFences, string.Empty);
    }
}

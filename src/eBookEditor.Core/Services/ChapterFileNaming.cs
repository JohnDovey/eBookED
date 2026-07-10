using System.Text.RegularExpressions;

namespace eBookEditor.Core.Services;

/// <summary>
/// Shared naming rules for chapter files on disk, used both when importing/dropping files
/// (to guess where a file like "23. What Now.md" belongs) and when syncing chapter file
/// names to match their resolved position (so "023-What-Now.ebhtml" sorts correctly in a file
/// browser, matching the book's actual order).
/// </summary>
public static partial class ChapterFileNaming
{
    [GeneratedRegex(@"^(\d+)[\s\.\-_:]+(.+)$")]
    private static partial Regex NumberedNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    /// <summary>
    /// Parses a leading number out of a chapter file's name (without extension), e.g.
    /// "23. What Now" -> (23, "What Now"), "007 - Foo" -> (7, "Foo"). Returns a null number
    /// and the trimmed original name when no leading number is present.
    /// </summary>
    public static (int? Number, string Title) ParseHint(string fileNameWithoutExtension)
    {
        var match = NumberedNameRegex().Match(fileNameWithoutExtension.Trim());
        if (match.Success && int.TryParse(match.Groups[1].Value, out var number))
            return (number, match.Groups[2].Value.Trim());

        return (null, fileNameWithoutExtension.Trim());
    }

    /// <summary>Builds the on-disk file name for a chapter at its resolved number, e.g. (23, "What Now") -> "023-What-Now.ebhtml".</summary>
    public static string BuildFileName(int? number, string title)
    {
        var safeTitle = SanitizeForFileName(title);
        return number is { } n ? $"{n:D3}-{safeTitle}.ebhtml" : $"{safeTitle}.ebhtml";
    }

    // Path.GetInvalidFileNameChars() only reflects the *current* OS's restrictions (on
    // macOS/Linux that's essentially just '/' and NUL), which would under-sanitize a title
    // for cross-platform use — a project authored on a Mac should still produce chapter file
    // names that are valid if the project directory is later opened on Windows. Blocklist the
    // union of what Windows, macOS, and Linux all forbid instead.
    private static readonly char[] CrossPlatformInvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static string SanitizeForFileName(string title)
    {
        var withInvalidCharsReplaced = new string(title.Select(c => CrossPlatformInvalidChars.Contains(c) || char.IsControl(c) ? '-' : c).ToArray()).Trim();

        // Chapter file names never contain spaces — filesystem hygiene, and one less thing to
        // percent-encode wherever a chapter's relative path ends up as an HTML href or asset
        // URL (EPUB content documents, the WYSIWYG editor's WebView, etc.).
        var withoutSpaces = WhitespaceRunRegex().Replace(withInvalidCharsReplaced, "-");
        while (withoutSpaces.Contains("--", StringComparison.Ordinal))
            withoutSpaces = withoutSpaces.Replace("--", "-");
        var sanitized = withoutSpaces.Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled" : sanitized;
    }
}

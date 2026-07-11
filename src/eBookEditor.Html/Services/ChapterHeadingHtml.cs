using System.Net;
using System.Text.RegularExpressions;
using eBookEditor.Core.Models;

namespace eBookEditor.Html.Services;

/// <summary>
/// Builds the "&lt;h1&gt;Chapter N: Title&lt;/h1&gt;&lt;h2&gt;Subtitle&lt;/h2&gt;" heading every
/// exporter (and the editor's own Preview/Rich Text rendering) prepends to a chapter's body at
/// render time — but only for an item whose body doesn't already open with its own &lt;h1&gt;.
/// Authoring turns out to go both ways: some chapters/pages rely entirely on the front-matter
/// title (SpineItem.Title/Subtitle) with no heading of their own in the body, others (confirmed
/// against this app's own real, hand-authored manual/ project) type a real "&lt;h1&gt;" directly
/// into the body, the same way any WYSIWYG editor works. Synthesizing unconditionally duplicated
/// the heading for the latter — this class exists so every renderer makes the same "does this
/// body already have one" call instead of each reimplementing (or forgetting) it.
///
/// Only the app's own fixed, always-auto-generated pages (SpineItem.IsGenerated — title page,
/// imprint, table of contents, about the author) are excluded outright, since
/// PageGeneratorService already bakes their heading directly into the generated body every time
/// it regenerates them. A user-added custom front/back-matter page (e.g. "Preface," "Afterword")
/// is IsGenerated=false, exactly like a chapter — its own typed title needs the same synthesis a
/// chapter gets, or it never appears anywhere in Preview/PDF/EPUB/Word at all, only in the
/// sidebar list and internal navigation (a real bug: a custom front-matter page titled
/// "Introduction" whose body doesn't start with its own heading rendered with no visible title
/// anywhere a reader would actually see it).
/// </summary>
public static partial class ChapterHeadingHtml
{
    public static string? Build(SpineItem item, string body)
    {
        if (item.IsGenerated || string.IsNullOrWhiteSpace(item.Title))
            return null;

        if (BodyStartsWithHeading().IsMatch(body))
            return null;

        var titleHtml = WebUtility.HtmlEncode(item.Title);

        if (item.Type != SpineItemType.Chapter)
            return $"<h1>{titleHtml}</h1>";

        var heading = item.ResolvedNumber is { } number
            ? $"<h1>Chapter {number}: {titleHtml}</h1>"
            : $"<h1>{titleHtml}</h1>";

        return string.IsNullOrWhiteSpace(item.Subtitle)
            ? heading
            : $"{heading}\n<h2>{WebUtility.HtmlEncode(item.Subtitle)}</h2>";
    }

    // Matches an <h1> (optionally with attributes) as the first real content of the body,
    // ignoring only leading whitespace — a chapter that opens with anything else (a <p>, an
    // <h2>, an image) is treated as not having its own heading, even if an <h1> shows up deeper
    // in the content for some unrelated reason.
    [GeneratedRegex(@"\A\s*<h1(\s[^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex BodyStartsWithHeading();
}

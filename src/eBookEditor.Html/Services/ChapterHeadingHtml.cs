using System.Net;
using System.Text.RegularExpressions;
using eBookEditor.Core.Models;

namespace eBookEditor.Html.Services;

/// <summary>
/// Builds the "&lt;h1&gt;Chapter N: Title&lt;/h1&gt;&lt;h2&gt;Subtitle&lt;/h2&gt;" heading every
/// exporter (and the editor's own Preview/Rich Text rendering) prepends to a chapter's body at
/// render time — but only for a chapter whose body doesn't already open with its own &lt;h1&gt;.
/// Chapter authoring turns out to go both ways: some chapters rely entirely on the front-matter
/// title (SpineItem.Title/Subtitle) with no heading of their own in the body, others (confirmed
/// against this app's own real, hand-authored manual/ project) type a real "&lt;h1&gt;" directly
/// into the body, the same way any WYSIWYG editor works. Synthesizing unconditionally duplicated
/// the heading for the latter — this class exists so every renderer makes the same "does this
/// body already have one" call instead of each reimplementing (or forgetting) it.
/// Front/back matter pages are excluded — PageGeneratorService already bakes their heading
/// directly into the generated body.
/// </summary>
public static partial class ChapterHeadingHtml
{
    public static string? Build(SpineItem item, string body)
    {
        if (item.Type != SpineItemType.Chapter || string.IsNullOrWhiteSpace(item.Title))
            return null;

        if (BodyStartsWithHeading().IsMatch(body))
            return null;

        var titleHtml = WebUtility.HtmlEncode(item.Title);
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

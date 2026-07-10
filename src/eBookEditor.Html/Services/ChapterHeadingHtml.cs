using System.Net;
using eBookEditor.Core.Models;

namespace eBookEditor.Html.Services;

/// <summary>
/// Builds the "&lt;h1&gt;Chapter N: Title&lt;/h1&gt;&lt;h2&gt;Subtitle&lt;/h2&gt;" heading every
/// exporter (and the editor's own Preview/Rich Text rendering) prepends to a chapter's body at
/// render time. Chapter files deliberately store the title/subtitle only in front matter, not in
/// the body (see ChapterFrontMatter, SpineItem.Title/Subtitle) — this is the one place that
/// synthesizes the visible heading from it, so every renderer produces the same result instead of
/// each reimplementing (or, as happened before this existed, forgetting to implement) it.
/// Front/back matter pages are excluded — PageGeneratorService already bakes their heading
/// directly into the generated body.
/// </summary>
public static class ChapterHeadingHtml
{
    public static string? Build(SpineItem item)
    {
        if (item.Type != SpineItemType.Chapter || string.IsNullOrWhiteSpace(item.Title))
            return null;

        var titleHtml = WebUtility.HtmlEncode(item.Title);
        var heading = item.ResolvedNumber is { } number
            ? $"<h1>Chapter {number}: {titleHtml}</h1>"
            : $"<h1>{titleHtml}</h1>";

        return string.IsNullOrWhiteSpace(item.Subtitle)
            ? heading
            : $"{heading}\n<h2>{WebUtility.HtmlEncode(item.Subtitle)}</h2>";
    }
}

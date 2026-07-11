using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>
/// The raw-mode half of "Mark as Index Entry…"'s "automatically mark all occurrences of this
/// text" checkbox (WYSIWYG mode's equivalent is HtmlPageShell's own markAllOccurrences JS
/// function) — wraps every case-insensitive occurrence of a term found in a chapter body's
/// actual text (never inside markup/attributes) in a new index-entry marker (see
/// InternalLinkConvention). Skips text already inside an existing index-entry span, so
/// re-running this after some occurrences are already marked doesn't double-wrap them.
/// </summary>
public static class IndexEntryMarker
{
    private static readonly HtmlParser Parser = new();

    public static string MarkAllOccurrences(string html, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return html;

        var document = Parser.ParseDocument($"<body>{html}</body>");
        var body = document.Body!;
        var baseSlug = Slug.Create(term, "term");
        var counter = 0;

        // Snapshot text nodes before mutating — walking and mutating the tree at the same time
        // risks skipping nodes the walker hasn't reached yet.
        var textNodes = body.Descendants<IText>().Where(t => !IsInsideIndexEntry(t)).ToList();

        foreach (var textNode in textNodes)
        {
            var text = textNode.Data;
            var occurrences = FindOccurrences(text, term);
            if (occurrences.Count == 0)
                continue;

            var parent = textNode.ParentElement;
            if (parent is null)
                continue;

            var lastEnd = 0;
            foreach (var (start, length) in occurrences)
            {
                if (start > lastEnd)
                    parent.InsertBefore(document.CreateTextNode(text[lastEnd..start]), textNode);

                var span = document.CreateElement("span");
                span.ClassList.Add(InternalLinkConvention.IndexEntryClass);
                span.SetAttribute(InternalLinkConvention.IndexTermDataAttribute, term);
                span.Id = $"{InternalLinkConvention.IndexEntryIdPrefix}{baseSlug}:{counter++}";
                span.TextContent = text.Substring(start, length);
                parent.InsertBefore(span, textNode);

                lastEnd = start + length;
            }

            if (lastEnd < text.Length)
                parent.InsertBefore(document.CreateTextNode(text[lastEnd..]), textNode);

            textNode.Remove();
        }

        return body.InnerHtml;
    }

    private static bool IsInsideIndexEntry(IText textNode)
    {
        for (var parent = textNode.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            if (parent.ClassList.Contains(InternalLinkConvention.IndexEntryClass))
                return true;
        }

        return false;
    }

    private static List<(int Start, int Length)> FindOccurrences(string text, string term)
    {
        var results = new List<(int, int)>();
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            results.Add((index, term.Length));
            index += term.Length;
        }

        return results;
    }
}

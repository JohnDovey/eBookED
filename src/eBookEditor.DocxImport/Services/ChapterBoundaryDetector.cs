using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;

namespace eBookEditor.DocxImport.Services;

internal static partial class ChapterBoundaryDetector
{
    // Anchored end-to-end so it matches standalone heading-style lines ("Chapter 3",
    // "Chapter 3: The Finale") without false-positiving on ordinary prose that merely
    // starts with "Chapter <word>" (e.g. "Chapter one content continued...").
    [GeneratedRegex(@"^(Chapter|CHAPTER)\s+(\d+|[A-Za-z]+)(:\s*.+)?$")]
    private static partial Regex ChapterTitleRegex();

    public static bool IsChapterBoundary(Paragraph paragraph)
    {
        if (IsHeading1(paragraph))
            return true;

        var text = GetPlainText(paragraph);
        return LooksLikeChapterTitle(text);
    }

    public static bool IsHeading1(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId is not null && styleId.Equals("Heading1", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Matches Word's built-in Table of Contents field entry styles ("TOC1", "TOC 1",
    /// "TOC2", …) — these paragraphs list chapter titles with page numbers and must never be
    /// treated as real chapter content or a chapter boundary.</summary>
    public static bool IsTocFieldEntry(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId is not null && styleId.Replace(" ", "").StartsWith("TOC", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A paragraph's own text is exactly a "Table of Contents"/"Contents" heading —
    /// the section header a hand-typed (as opposed to Word-field-generated) TOC list of
    /// chapter titles would normally sit under.</summary>
    public static bool IsTableOfContentsHeading(Paragraph paragraph)
    {
        var text = GetPlainText(paragraph).Trim();
        return text.Equals("Table of Contents", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Contents", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeChapterTitle(string text) => ChapterTitleRegex().IsMatch(text.Trim());

    public static bool IsSubheading(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        return styleId is not null &&
               (styleId.Equals("Heading2", StringComparison.OrdinalIgnoreCase) ||
                styleId.Equals("Heading3", StringComparison.OrdinalIgnoreCase));
    }

    public static string GetPlainText(Paragraph paragraph) =>
        string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
}

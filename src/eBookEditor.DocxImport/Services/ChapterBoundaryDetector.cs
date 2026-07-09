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
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is not null && styleId.Equals("Heading1", StringComparison.OrdinalIgnoreCase))
            return true;

        var text = GetPlainText(paragraph);
        return ChapterTitleRegex().IsMatch(text.Trim());
    }

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

using eBookEditor.Core.Services;

namespace eBookEditor.Pdf.Services;

/// <summary>Formats a physical PDF page number as the label a reader would actually see:
/// lowercase roman numerals for front matter, arabic numerals (reset to 1) from the first
/// chapter onward — the standard print-book convention. Shared by the header and footer
/// dynamic components so both agree on the same page label.</summary>
internal static class PdfPageNumberFormatter
{
    public static string Format(int pageNumber, int frontMatterPageCount) =>
        pageNumber <= frontMatterPageCount
            ? RomanNumerals.ToLowerRoman(pageNumber)
            : (pageNumber - frontMatterPageCount).ToString();
}

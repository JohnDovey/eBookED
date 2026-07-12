using System.Net;
using System.Text;
using eBookEditor.Core.Services;

namespace eBookEditor.Html.Services;

/// <summary>One picked file for a gallery, with the pixel dimensions read from the file itself
/// (before any page-fit scaling) and a default caption (the filename without its extension,
/// same default a single "Insert Image…" already uses).</summary>
public sealed record GalleryImageSelection(string FileName, int NaturalWidth, int NaturalHeight, string DefaultCaption);

/// <summary>
/// Builds the HTML for "Insert as Gallery" (see MainWindow.OnInsertImageClick) — a
/// "table.gallery" grid, ColumnsPerRow images per row, each cell its own captioned
/// &lt;figure&gt; with a real InternalLinkConvention.FigureIdPrefix id, exactly like a normal
/// single-image insert — so right-click "Edit Image…" and the List of Figures page both work on
/// each gallery image individually, with no gallery-specific case needed in either.
///
/// A real HTML &lt;table&gt; is deliberately reused rather than a CSS grid/flexbox layout:
/// EPUB/WYSIWYG/Preview render either fine, but PDF/Word can only reuse their EXISTING table
/// renderers (HtmlToPdfRenderer.RenderTable/HtmlToDocxConverter.AppendTable, both extended to
/// recognize a "gallery" table's cells as figures rather than plain text) if the source markup
/// is an actual table — and QuestPDF's Table component breaks rows cleanly across pages on its
/// own, which is exactly the "rows should break nicely across pages" requirement this was built
/// for; a CSS grid has no equivalent for PDF at all.
/// </summary>
public static class GalleryHtmlBuilder
{
    public const int ColumnsPerRow = 3;
    public const int MaxImages = 20;

    /// <summary>Matches PdfBuilder's own hardcoded page margin (see InsertImageWindow, which
    /// uses the same constant for its own single-image page-fit clamp).</summary>
    private const double MarginInches = 0.75;

    /// <summary>CSS/HTML's standard reference pixel density — the same assumption every other
    /// width/height attribute this app writes into generated HTML already relies on.</summary>
    private const double PixelsPerInch = 96;

    /// <summary>Matches the "gallery" CSS class's own cell padding (see DefaultStylesheet.cs/
    /// "Vellum Serif.css"/RoyalRoad.css) — subtracted from each column's raw share of the page
    /// width so an image doesn't render flush against its neighbor or the page margin.</summary>
    private const int CellPaddingAllowancePx = 16;

    /// <summary>The shared column width every gallery image is scaled to, computed once from
    /// the project's page size — each image then gets its own height from ITS OWN natural
    /// aspect ratio at that width, so a portrait photo next to a landscape one in the same row
    /// isn't stretched to match.</summary>
    public static int ComputeColumnWidthPx(PdfPageSizeOption pageSize)
    {
        var printableWidthPx = Math.Max(1, (pageSize.WidthInches - 2 * MarginInches) * PixelsPerInch);
        return Math.Max(1, (int)(printableWidthPx / ColumnsPerRow) - CellPaddingAllowancePx);
    }

    public static string Build(IReadOnlyList<GalleryImageSelection> images, PdfPageSizeOption pageSize)
    {
        var columnWidthPx = ComputeColumnWidthPx(pageSize);
        var sb = new StringBuilder();
        sb.AppendLine("<table class=\"gallery\">");

        for (var i = 0; i < images.Count; i += ColumnsPerRow)
        {
            sb.AppendLine("<tr>");
            for (var col = 0; col < ColumnsPerRow; col++)
            {
                if (i + col >= images.Count)
                {
                    sb.AppendLine("<td></td>");
                    continue;
                }

                var image = images[i + col];
                var height = Math.Max(1, (int)Math.Round(columnWidthPx * (image.NaturalHeight / (double)image.NaturalWidth)));
                var figureId = $"{InternalLinkConvention.FigureIdPrefix}{Guid.NewGuid():N}";
                var caption = Encode(image.DefaultCaption);
                sb.AppendLine($"""
                    <td><figure id="{figureId}"><img src="../images/{Encode(image.FileName)}" alt="{caption}" width="{columnWidthPx}" height="{height}"><figcaption class="caption">{caption}</figcaption></figure></td>
                    """);
            }
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}

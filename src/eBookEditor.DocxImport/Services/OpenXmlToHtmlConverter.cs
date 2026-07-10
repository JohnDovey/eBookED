using System.Net;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace eBookEditor.DocxImport.Services;

internal enum ParagraphKind { Empty, Paragraph, Subheading, ListItem }

/// <summary>A converted paragraph's HTML and enough shape information for the caller
/// (DocxImportService) to group consecutive ListItem paragraphs into a wrapping
/// &lt;ul&gt;/&lt;ol&gt; — a single paragraph only knows about itself, not its neighbors.</summary>
internal readonly record struct ConvertedParagraph(string Html, ParagraphKind Kind, bool Ordered = false)
{
    public static readonly ConvertedParagraph Empty = new("", ParagraphKind.Empty);
}

/// <summary>
/// Covers the common manuscript formatting cases (bold/italic, H2/H3 subheadings, simple
/// single-level bullet/numbered lists, inline images, hyperlinks, and tables), emitting HTML
/// fragments instead of Markdown.
/// </summary>
internal class OpenXmlToHtmlConverter
{
    public string ConvertTable(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<table>\n<thead>\n<tr>");
        foreach (var cell in ExtractCells(rows[0]))
            sb.Append("<th>").Append(cell).Append("</th>");
        sb.Append("</tr>\n</thead>\n<tbody>\n");

        foreach (var row in rows.Skip(1))
        {
            sb.Append("<tr>");
            foreach (var cell in ExtractCells(row))
                sb.Append("<td>").Append(cell).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>");
        return sb.ToString();
    }

    private static List<string> ExtractCells(TableRow row) => row.Elements<TableCell>()
        .Select(cell => string.Join("<br>", cell.Elements<Paragraph>()
                .Select(p => Encode(string.Concat(p.Descendants<Text>().Select(t => t.Text)).Trim()))
                .Where(text => text.Length > 0)))
        .ToList();

    public ConvertedParagraph ConvertParagraph(Paragraph paragraph, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var runHtml = ConvertRuns(paragraph, mainPart, images);
        if (string.IsNullOrWhiteSpace(runHtml))
            return ConvertedParagraph.Empty;

        if (ChapterBoundaryDetector.IsSubheading(paragraph))
        {
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var level = string.Equals(styleId, "Heading2", StringComparison.OrdinalIgnoreCase) ? "h2" : "h3";
            return new ConvertedParagraph($"<{level}>{runHtml.Trim()}</{level}>", ParagraphKind.Subheading);
        }

        var numberingProperties = paragraph.ParagraphProperties?.NumberingProperties;
        if (numberingProperties is not null)
        {
            var ordered = IsOrderedList(mainPart, numberingProperties);
            return new ConvertedParagraph($"<li>{runHtml.Trim()}</li>", ParagraphKind.ListItem, ordered);
        }

        return new ConvertedParagraph($"<p>{runHtml}</p>", ParagraphKind.Paragraph);
    }

    private static string ConvertRuns(Paragraph paragraph, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var sb = new StringBuilder();

        foreach (var child in paragraph.ChildElements)
        {
            switch (child)
            {
                case Hyperlink hyperlink:
                    sb.Append(ConvertHyperlink(hyperlink, mainPart, images));
                    break;
                case Run run:
                    sb.Append(ConvertRun(run, mainPart, images));
                    break;
            }
        }

        return sb.ToString();
    }

    private static string ConvertHyperlink(Hyperlink hyperlink, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var text = string.Concat(hyperlink.Elements<Run>().Select(r => ConvertRun(r, mainPart, images)));
        if (text.Length == 0)
            return string.Empty;

        // Internal bookmark links (Anchor, no relationship Id) have nowhere meaningful to
        // point in a standalone chapter, so only the link text is kept.
        var relationshipId = hyperlink.Id?.Value;
        var url = relationshipId is null
            ? null
            : mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == relationshipId)?.Uri.ToString();

        return url is null ? text : $"<a href=\"{Encode(url)}\">{text}</a>";
    }

    private static string ConvertRun(Run run, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var drawing = run.Elements<Drawing>().FirstOrDefault();
        var imageHtml = drawing is not null ? TryExtractImage(drawing, mainPart, images) : null;
        if (imageHtml is not null)
            return imageHtml;

        var text = string.Concat(run.Elements<Text>().Select(t => t.Text));
        if (text.Length == 0)
            return string.Empty;

        var encoded = Encode(text);
        var bold = run.RunProperties?.Bold is not null && run.RunProperties.Bold.Val?.Value != false;
        var italic = run.RunProperties?.Italic is not null && run.RunProperties.Italic.Val?.Value != false;

        return bold && italic ? $"<strong><em>{encoded}</em></strong>" : bold ? $"<strong>{encoded}</strong>" : italic ? $"<em>{encoded}</em>" : encoded;
    }

    private static string? TryExtractImage(Drawing drawing, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        if (blip?.Embed?.Value is not { } relId)
            return null;

        if (mainPart.GetPartById(relId) is not ImagePart imagePart)
            return null;

        using var stream = imagePart.GetStream();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        var ext = Path.GetExtension(imagePart.Uri.OriginalString).TrimStart('.');
        if (string.IsNullOrEmpty(ext))
            ext = "png";

        var fileName = $"image-{images.Count + 1}.{ext}";
        images.Add(new ExtractedImage(fileName, memoryStream.ToArray()));

        return $"<img src=\"../images/{fileName}\" alt=\"\">";
    }

    private static bool IsOrderedList(MainDocumentPart mainPart, NumberingProperties numberingProperties)
    {
        var numId = numberingProperties.NumberingId?.Val?.Value;
        var numberingPart = mainPart.NumberingDefinitionsPart;
        if (numId is null || numberingPart?.Numbering is null)
            return false;

        var numberingInstance = numberingPart.Numbering.Elements<NumberingInstance>()
            .FirstOrDefault(n => n.NumberID?.Value == numId);
        var abstractNumId = numberingInstance?.AbstractNumId?.Val?.Value;
        if (abstractNumId is null)
            return false;

        var abstractNum = numberingPart.Numbering.Elements<AbstractNum>()
            .FirstOrDefault(a => a.AbstractNumberId?.Value == abstractNumId);
        var firstLevel = abstractNum?.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == 0);

        return firstLevel?.NumberingFormat?.Val?.Value == NumberFormatValues.Decimal;
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}

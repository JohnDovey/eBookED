using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Covers the common manuscript formatting cases (bold/italic, H2/H3 subheadings, simple
/// single-level bullet/numbered lists, inline images, hyperlinks, and tables).
/// </summary>
internal class OpenXmlToMarkdownConverter
{
    public string ConvertTable(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var headerCells = ExtractCells(rows[0]);
        sb.Append("| ").Append(string.Join(" | ", headerCells)).Append(" |").AppendLine();
        sb.Append("| ").Append(string.Join(" | ", headerCells.Select(_ => "---"))).Append(" |").AppendLine();

        foreach (var row in rows.Skip(1))
        {
            var cells = ExtractCells(row);
            sb.Append("| ").Append(string.Join(" | ", cells)).Append(" |").AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static List<string> ExtractCells(TableRow row) => row.Elements<TableCell>()
        .Select(cell => string.Join("<br>", cell.Elements<Paragraph>()
                .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)).Trim())
                .Where(text => text.Length > 0))
            .Replace("|", "\\|"))
        .ToList();

    public string ConvertParagraph(Paragraph paragraph, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var runText = ConvertRuns(paragraph, mainPart, images);
        if (string.IsNullOrWhiteSpace(runText))
            return string.Empty;

        if (ChapterBoundaryDetector.IsSubheading(paragraph))
        {
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var prefix = string.Equals(styleId, "Heading2", StringComparison.OrdinalIgnoreCase) ? "## " : "### ";
            return prefix + runText.Trim();
        }

        var numberingProperties = paragraph.ParagraphProperties?.NumberingProperties;
        if (numberingProperties is not null)
        {
            var marker = IsOrderedList(mainPart, numberingProperties) ? "1. " : "- ";
            return marker + runText.Trim();
        }

        return runText;
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
        // point in a standalone Markdown/EPUB chapter, so only the link text is kept.
        var relationshipId = hyperlink.Id?.Value;
        var url = relationshipId is null
            ? null
            : mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == relationshipId)?.Uri.ToString();

        return url is null ? text : $"[{text}]({url})";
    }

    private static string ConvertRun(Run run, MainDocumentPart mainPart, List<ExtractedImage> images)
    {
        var drawing = run.Elements<Drawing>().FirstOrDefault();
        var imageMarkdown = drawing is not null ? TryExtractImage(drawing, mainPart, images) : null;
        if (imageMarkdown is not null)
            return imageMarkdown;

        var text = string.Concat(run.Elements<Text>().Select(t => t.Text));
        if (text.Length == 0)
            return string.Empty;

        var bold = run.RunProperties?.Bold is not null && run.RunProperties.Bold.Val?.Value != false;
        var italic = run.RunProperties?.Italic is not null && run.RunProperties.Italic.Val?.Value != false;

        return bold && italic ? $"***{text}***" : bold ? $"**{text}**" : italic ? $"*{text}*" : text;
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

        return $"![](../images/{fileName})";
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
}

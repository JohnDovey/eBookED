using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Models;
using A = DocumentFormat.OpenXml.Drawing;

namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Covers the common manuscript formatting cases (bold/italic, H2/H3 subheadings, simple
/// single-level bullet/numbered lists, inline images). Tables and hyperlink targets are
/// intentionally out of scope for this first pass — hyperlink text still comes through as
/// plain text, and table content is skipped rather than mis-rendered.
/// </summary>
internal class OpenXmlToMarkdownConverter
{
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

        foreach (var run in paragraph.Descendants<Run>())
        {
            var drawing = run.Elements<Drawing>().FirstOrDefault();
            var imageMarkdown = drawing is not null ? TryExtractImage(drawing, mainPart, images) : null;
            if (imageMarkdown is not null)
            {
                sb.Append(imageMarkdown);
                continue;
            }

            var text = string.Concat(run.Elements<Text>().Select(t => t.Text));
            if (text.Length == 0)
                continue;

            var bold = run.RunProperties?.Bold is not null && run.RunProperties.Bold.Val?.Value != false;
            var italic = run.RunProperties?.Italic is not null && run.RunProperties.Italic.Val?.Value != false;

            sb.Append(bold && italic ? $"***{text}***" : bold ? $"**{text}**" : italic ? $"*{text}*" : text);
        }

        return sb.ToString();
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

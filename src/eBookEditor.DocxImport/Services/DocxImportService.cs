using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.DocxImport.Models;

namespace eBookEditor.DocxImport.Services;

public class DocxImportService
{
    public IReadOnlyList<ChapterDraft> Import(string docxPath)
    {
        using var document = WordprocessingDocument.Open(docxPath, false);
        var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The document has no main content part.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The document has no body.");

        var converter = new OpenXmlToMarkdownConverter();
        var chapters = new List<ChapterDraft>();

        string? currentTitle = null;
        var currentBody = new StringBuilder();
        var currentImages = new List<ExtractedImage>();

        void FlushCurrent()
        {
            if (currentTitle is null)
                return;

            chapters.Add(new ChapterDraft(currentTitle, currentBody.ToString().Trim(), currentImages));
            currentBody = new StringBuilder();
            currentImages = [];
        }

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph paragraph && ChapterBoundaryDetector.IsChapterBoundary(paragraph))
            {
                FlushCurrent();
                currentTitle = ExtractChapterTitle(paragraph);
                continue;
            }

            var line = element switch
            {
                Paragraph p => converter.ConvertParagraph(p, mainPart, currentImages),
                Table table => converter.ConvertTable(table),
                _ => null
            };

            if (string.IsNullOrEmpty(line))
                continue;

            // Content appearing before the first detected chapter heading is kept as an
            // implicit "Introduction" chapter rather than silently dropped.
            currentTitle ??= "Introduction";

            currentBody.AppendLine(line);
            currentBody.AppendLine();
        }

        FlushCurrent();
        return chapters;
    }

    private static string ExtractChapterTitle(Paragraph paragraph)
    {
        var text = ChapterBoundaryDetector.GetPlainText(paragraph).Trim();
        return string.IsNullOrEmpty(text) ? "Untitled Chapter" : text;
    }
}

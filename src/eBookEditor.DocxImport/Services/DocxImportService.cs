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
        // Whether we're inside a hand-typed "Table of Contents" list (as opposed to Word's own
        // field-generated one, which is filtered out below by paragraph style alone). Every
        // entry in such a list reads exactly like a chapter heading ("Chapter 1: Getting
        // Ready"), which would otherwise each get treated as the start of a new, empty chapter.
        var skippingHandTypedToc = false;

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
            if (element is Paragraph paragraph)
            {
                // Word's own Insert > Table of Contents field renders each entry in a "TOC1"/
                // "TOC2"/… styled paragraph — never chapter content, regardless of what its text
                // looks like.
                if (ChapterBoundaryDetector.IsTocFieldEntry(paragraph))
                    continue;

                if (ChapterBoundaryDetector.IsTableOfContentsHeading(paragraph))
                {
                    skippingHandTypedToc = true;
                    continue;
                }

                if (skippingHandTypedToc)
                {
                    // A real chapter heading (Heading1) always ends the list, even if its own
                    // text also happens to look like a TOC entry. Otherwise, keep skipping
                    // blank lines and chapter-title-shaped lines; the first paragraph that's
                    // neither is real prose, meaning the TOC list is over.
                    if (!ChapterBoundaryDetector.IsHeading1(paragraph))
                    {
                        var tocLineText = ChapterBoundaryDetector.GetPlainText(paragraph).Trim();
                        if (tocLineText.Length == 0 || ChapterBoundaryDetector.LooksLikeChapterTitle(tocLineText))
                            continue;
                    }
                    skippingHandTypedToc = false;
                }

                if (ChapterBoundaryDetector.IsChapterBoundary(paragraph))
                {
                    FlushCurrent();
                    currentTitle = ExtractChapterTitle(paragraph);
                    continue;
                }
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

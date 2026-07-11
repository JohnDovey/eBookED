using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.Core.Models;
using eBookEditor.DocxImport.Models;

namespace eBookEditor.DocxImport.Services;

public class DocxImportService
{
    public IReadOnlyList<ChapterDraft> Import(string docxPath)
    {
        using var document = WordprocessingDocument.Open(docxPath, false);
        var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The document has no main content part.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The document has no body.");

        var converter = new OpenXmlToHtmlConverter();
        var chapters = new List<ChapterDraft>();

        string? currentTitle = null;
        var currentClassification = (Type: SpineItemType.Chapter, NumberMode: ChapterNumberMode.Auto);
        var currentBody = new StringBuilder();
        var currentImages = new List<ExtractedImage>();
        // Whether we're inside a hand-typed "Table of Contents" list (as opposed to Word's own
        // field-generated one, which is filtered out below by paragraph style alone). Every
        // entry in such a list reads exactly like a chapter heading ("Chapter 1: Getting
        // Ready"), which would otherwise each get treated as the start of a new, empty chapter.
        var skippingHandTypedToc = false;
        // Consecutive ListItem paragraphs of the same ordered/unordered kind are grouped into
        // one wrapping <ul>/<ol> — null when no list is currently open, else "ul" or "ol".
        string? openListTag = null;

        void CloseOpenList()
        {
            if (openListTag is null)
                return;

            currentBody.AppendLine($"</{openListTag}>");
            currentBody.AppendLine();
            openListTag = null;
        }

        void FlushCurrent()
        {
            if (currentTitle is null)
                return;

            CloseOpenList();
            chapters.Add(new ChapterDraft(currentTitle, currentBody.ToString().Trim(), currentImages, currentClassification.Type, currentClassification.NumberMode));
            currentBody = new StringBuilder();
            currentImages = [];
            currentClassification = (SpineItemType.Chapter, ChapterNumberMode.Auto);
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
                    currentClassification = SpecialPageClassifier.Classify(currentTitle);
                    continue;
                }

                var converted = converter.ConvertParagraph(paragraph, mainPart, currentImages);
                if (converted.Kind == ParagraphKind.Empty)
                    continue;

                // Content appearing before the first detected chapter heading is kept as an
                // implicit "Introduction" chapter rather than silently dropped.
                if (converted.Kind == ParagraphKind.ListItem)
                {
                    var wantedTag = converted.Ordered ? "ol" : "ul";
                    if (openListTag is not null && openListTag != wantedTag)
                        CloseOpenList();

                    if (openListTag is null)
                    {
                        currentTitle ??= "Introduction";
                        currentBody.AppendLine($"<{wantedTag}>");
                        openListTag = wantedTag;
                    }

                    currentBody.AppendLine(converted.Html);
                    continue;
                }

                CloseOpenList();
                currentTitle ??= "Introduction";
                currentBody.AppendLine(converted.Html);
                currentBody.AppendLine();
            }
            else if (element is Table table)
            {
                var html = converter.ConvertTable(table);
                if (string.IsNullOrEmpty(html))
                    continue;

                CloseOpenList();
                currentTitle ??= "Introduction";
                currentBody.AppendLine(html);
                currentBody.AppendLine();
            }
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

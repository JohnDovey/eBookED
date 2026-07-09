using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>A chapter this header can name on right-hand pages, and the id its opening
/// element was tagged with via <c>.CaptureContentPosition(id)</c> so this header can look up
/// which physical page it started on.</summary>
internal record PdfHeaderChapter(string Label, string CaptureId);

/// <summary>
/// Page header with the standard print-book left/right (verso/recto) layout: even
/// ("left"/verso) pages show the page number on the left and the book's title on the right;
/// odd ("right"/recto) pages show the current chapter's number and name on the left and the
/// page number on the right. "Current chapter" for a given page is resolved by checking,
/// for each chapter in order, the physical page its opening element was captured on (via
/// <c>.CaptureContentPosition</c>, tagged in MarkdownToPdfRenderer using the same id as its
/// TOC Section) — the last chapter whose captured page is at or before the page being
/// rendered is the one currently open. QuestPDF's dynamic-component context has no built-in
/// "what section is active on this page" query (only forward lookups like
/// BeginPageNumberOfSection, used from Text content), so this is assembled from the same
/// position-capture primitive the library exposes for that purpose.
/// </summary>
internal class PdfPageHeader(int frontMatterPageCount, string bookTitle, IReadOnlyList<PdfHeaderChapter> chapters) : IDynamicComponent
{
    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        var isLeftPage = context.PageNumber % 2 == 0;
        var pageLabel = PdfPageNumberFormatter.Format(context.PageNumber, frontMatterPageCount);

        var content = context.CreateElement(element =>
        {
            element.Row(row =>
            {
                if (isLeftPage)
                {
                    Cell(row.RelativeItem(), pageLabel, alignRight: false);
                    Cell(row.RelativeItem(), bookTitle, alignRight: true);
                }
                else
                {
                    Cell(row.RelativeItem(), CurrentChapterLabel(context), alignRight: false);
                    Cell(row.RelativeItem(), pageLabel, alignRight: true);
                }
            });
        });

        return new DynamicComponentComposeResult { Content = content, HasMoreContent = false };
    }

    // QuestPDF's text layout throws on a zero-length string (e.g. no chapter has started yet
    // on an early front-matter page, or the book has no title), so skip rendering entirely
    // rather than pass it an empty string.
    private static void Cell(IContainer container, string text, bool alignRight)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var aligned = alignRight ? container.AlignRight() : container;
        aligned.Text(text).FontSize(9);
    }

    private string CurrentChapterLabel(DynamicContext context)
    {
        string? label = null;

        foreach (var chapter in chapters)
        {
            var startPage = context.GetContentCapturedPositions(chapter.CaptureId).FirstOrDefault()?.PageNumber;
            if (startPage is { } page && page <= context.PageNumber)
                label = chapter.Label;
        }

        return label ?? string.Empty;
    }
}

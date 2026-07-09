using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Page footer with the standard print-book left/right (verso/recto) layout: even
/// ("left"/verso) pages show the page number on the left and the author's name on the right;
/// odd ("right"/recto) pages show only the page number, on the right. Front matter pages are
/// numbered with lowercase roman numerals and body pages with arabic numerals reset to 1 —
/// see <see cref="PdfPageNumberFormatter"/>. Assumes each front-matter spine item renders as
/// exactly one physical page, which holds for the short generated pages (title/imprint/TOC)
/// this app produces; a very long hand-authored front-matter page would throw off the
/// numbering for pages after it. Also captures <see cref="TotalPages"/> for the caller to
/// read once generation finishes, since QuestPDF has no separate "count pages" step.
/// </summary>
internal class FrontMatterAwarePageNumberFooter(int frontMatterPageCount, string authorName) : IDynamicComponent
{
    public int TotalPages { get; private set; }

    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        TotalPages = context.TotalPages;

        var isLeftPage = context.PageNumber % 2 == 0;
        var pageLabel = PdfPageNumberFormatter.Format(context.PageNumber, frontMatterPageCount);

        var content = context.CreateElement(element =>
        {
            element.Row(row =>
            {
                if (isLeftPage)
                {
                    Cell(row.RelativeItem(), pageLabel, alignRight: false);
                    Cell(row.RelativeItem(), authorName, alignRight: true);
                }
                else
                {
                    row.RelativeItem();
                    Cell(row.RelativeItem(), pageLabel, alignRight: true);
                }
            });
        });

        return new DynamicComponentComposeResult { Content = content, HasMoreContent = false };
    }

    // QuestPDF's text layout throws on a zero-length string (e.g. a book with no author set),
    // so skip rendering entirely rather than pass it an empty string.
    private static void Cell(IContainer container, string text, bool alignRight)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var aligned = alignRight ? container.AlignRight() : container;
        aligned.Text(text).FontSize(9);
    }
}

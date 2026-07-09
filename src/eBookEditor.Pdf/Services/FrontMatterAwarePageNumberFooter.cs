using eBookEditor.Core.Services;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Page-number footer that numbers front matter pages with lowercase roman numerals and
/// switches to arabic numerals from the first chapter onward — the standard print-book
/// convention. Assumes each front-matter spine item renders as exactly one physical page,
/// which holds for the short generated pages (title/imprint/TOC) this app produces; a very
/// long hand-authored front-matter page would throw off the numbering for pages after it.
/// Also captures <see cref="TotalPages"/> for the caller to read once generation finishes,
/// since QuestPDF has no separate "count pages" step.
/// </summary>
internal class FrontMatterAwarePageNumberFooter(int frontMatterPageCount) : IDynamicComponent
{
    public int TotalPages { get; private set; }

    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        TotalPages = context.TotalPages;

        var label = context.PageNumber <= frontMatterPageCount
            ? RomanNumerals.ToLowerRoman(context.PageNumber)
            : (context.PageNumber - frontMatterPageCount).ToString();

        var content = context.CreateElement(element => element.AlignCenter().Text(label).FontSize(9));

        return new DynamicComponentComposeResult { Content = content, HasMoreContent = false };
    }
}

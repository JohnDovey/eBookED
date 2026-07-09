using eBookEditor.Core.Services;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

internal static class PdfPageSizeResolver
{
    public static PageSize Resolve(string? name)
    {
        var option = PdfPageSizeCatalog.Resolve(name);
        return new PageSize((float)option.WidthInches, (float)option.HeightInches, Unit.Inch);
    }
}

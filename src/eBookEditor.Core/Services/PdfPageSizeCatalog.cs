namespace eBookEditor.Core.Services;

/// <summary>A named page size and its physical dimensions in inches. Lives in Core (no PDF
/// library dependency) so both the settings UI and the PDF builder can share one definitive
/// list — the builder maps a name to an actual PDF page size via <see cref="WidthInches"/>/
/// <see cref="HeightInches"/> rather than this project needing to know anything about PDFs.</summary>
public record PdfPageSizeOption(string Name, double WidthInches, double HeightInches);

public static class PdfPageSizeCatalog
{
    public const string DefaultName = "A5";

    public static readonly IReadOnlyList<PdfPageSizeOption> All =
    [
        new("A5", 5.83, 8.27),
        new("A4", 8.27, 11.69),
        new("US Letter", 8.5, 11),
        new("US Trade (6 x 9 in)", 6, 9),
        new("Digest (5.5 x 8.5 in)", 5.5, 8.5),
        new("Mass Market Paperback (4.25 x 6.87 in)", 4.25, 6.87),
    ];

    public static PdfPageSizeOption Resolve(string? name) =>
        All.FirstOrDefault(o => o.Name == name) ?? All.First(o => o.Name == DefaultName);
}

using System.Text.RegularExpressions;
using eBookEditor.Epub.Services;
using QuestPDF.Drawing;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Wires PDF export to a book's selected CSS template: registers the @font-face fonts it
/// references with QuestPDF's font engine — the same files FontService/EpubBuilder embed
/// into the EPUB — so QuestPDF embeds them into the generated PDF too, and resolves the
/// body/heading font families the template actually asks for instead of PDF export being
/// stuck on a fixed default typeface. Falls back to <see cref="DefaultFontFamily"/> for any
/// family the template names but doesn't back with a shipped font file (e.g. the "Default"
/// template's generic "serif" family, or a web-safe font like "Palatino" with no @font-face
/// rule) — QuestPDF/Skia can't embed a font it was never given bytes for.
/// </summary>
public partial class PdfTemplateFonts
{
    public const string DefaultFontFamily = "Times New Roman";

    [GeneratedRegex(@"([^{}]+)\{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex RuleBlockRegex();

    [GeneratedRegex("""font-family\s*:\s*([^;]+);""")]
    private static partial Regex FontFamilyDeclarationRegex();

    private readonly FontService _fontService;

    public PdfTemplateFonts(FontService? fontService = null)
    {
        _fontService = fontService ?? new FontService();
    }

    public record ResolvedFonts(string BodyFontFamily, string HeadingFontFamily);

    /// <summary>
    /// Registers every embeddable @font-face font the template references with QuestPDF,
    /// then resolves which registered family (if any) the template's body/h1 rules ask for.
    /// </summary>
    public ResolvedFonts RegisterAndResolve(string templateCss)
    {
        var registeredFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fontFace in _fontService.ParseFontFaces(templateCss))
        {
            var fontPath = _fontService.ResolveFontFilePath(fontFace.FileName);
            if (fontPath is null)
                continue;

            using var stream = File.OpenRead(fontPath);
            FontManager.RegisterFontWithCustomName(fontFace.FontFamily, stream);
            registeredFamilies.Add(fontFace.FontFamily);
        }

        var bodyFamily = ExtractFirstFontFamily(templateCss, "body");
        var resolvedBody = bodyFamily is not null && registeredFamilies.Contains(bodyFamily)
            ? bodyFamily
            : DefaultFontFamily;

        var headingFamily = ExtractFirstFontFamily(templateCss, "h1");
        var resolvedHeading = headingFamily is not null && registeredFamilies.Contains(headingFamily)
            ? headingFamily
            : resolvedBody;

        return new ResolvedFonts(resolvedBody, resolvedHeading);
    }

    private static string? ExtractFirstFontFamily(string css, string selectorName)
    {
        foreach (Match rule in RuleBlockRegex().Matches(css))
        {
            var selectors = rule.Groups[1].Value.Split(',').Select(s => s.Trim());
            if (!selectors.Contains(selectorName, StringComparer.OrdinalIgnoreCase))
                continue;

            var familyMatch = FontFamilyDeclarationRegex().Match(rule.Groups[2].Value);
            if (!familyMatch.Success)
                continue;

            return familyMatch.Groups[1].Value.Split(',')[0].Trim().Trim('"', '\'');
        }

        return null;
    }
}

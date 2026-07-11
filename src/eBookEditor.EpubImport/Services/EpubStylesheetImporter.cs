using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using eBookEditor.Epub.Services;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Builds a new CSS template from a source EPUB's own stylesheet(s), structured exactly like
/// "Vellum Serif.css" — every selector Vellum defines, in Vellum's own order — using the
/// source's declarations wherever it defines an equivalent selector (exact selector-text
/// match, not fuzzy class-name guessing: real EPUBs consistently style plain structural
/// elements like body/h1/p for baseline readability, and this app's own EditorStyleCatalog
/// classes essentially never exist in a foreign EPUB's CSS, so they correctly and consistently
/// fall back to Vellum's declaration), falling back to Vellum's original declaration for
/// anything the source doesn't define. A pure function — no zip/file I/O of its own; the
/// caller (EpubProjectImporter) supplies already-read stylesheet text/font bytes and writes
/// whatever this returns to disk.
/// </summary>
public static partial class EpubStylesheetImporter
{
    [GeneratedRegex("""font-family\s*:\s*([^;]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex FontFamilyDeclarationRegex();

    [GeneratedRegex(@"@font-face\s*\{[^}]*\}", RegexOptions.Singleline)]
    private static partial Regex FontFaceBlockRegex();

    public static (string MergedCss, IReadOnlyDictionary<string, byte[]> FontsToWrite) BuildTemplate(
        IReadOnlyList<string> sourceStylesheets,
        IReadOnlyDictionary<string, byte[]> sourceFontsByFileName,
        string vellumCss)
    {
        var parser = new CssParser();
        var vellumRules = ParseStyleRules(parser, vellumCss);

        var sourceRulesBySelector = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sheet in sourceStylesheets)
            foreach (var (selector, declarations) in ParseStyleRules(parser, sheet))
                sourceRulesBySelector[selector] = declarations;

        var css = new StringBuilder();
        var referencedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (selector, vellumDeclarations) in vellumRules)
        {
            var declarations = sourceRulesBySelector.GetValueOrDefault(selector, vellumDeclarations);
            css.Append(selector).Append(" {\n    ").Append(declarations).Append("\n}\n\n");

            foreach (Match match in FontFamilyDeclarationRegex().Matches(declarations))
            {
                var firstFamily = match.Groups[1].Value.Split(',')[0].Trim().Trim('"', '\'');
                if (firstFamily.Length > 0)
                    referencedFamilies.Add(firstFamily);
            }
        }

        var sourceFontFaces = new FontService().ParseFontFaces(string.Join("\n", sourceStylesheets));
        var vellumFontFaceBlocksByFamily = ExtractFontFaceBlocksByFamily(vellumCss);

        var fontFacesCss = new StringBuilder();
        var fontsToWrite = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var family in referencedFamilies)
        {
            var sourceFontFace = sourceFontFaces.FirstOrDefault(f => f.FontFamily.Equals(family, StringComparison.OrdinalIgnoreCase));
            if (sourceFontFace is not null && sourceFontsByFileName.TryGetValue(sourceFontFace.FileName, out var fontBytes))
            {
                fontsToWrite[sourceFontFace.FileName] = fontBytes;
                fontFacesCss.Append(BuildFontFaceBlock(sourceFontFace.FontFamily, sourceFontFace.FileName)).Append('\n');
                continue;
            }

            if (vellumFontFaceBlocksByFamily.TryGetValue(family, out var vellumBlock))
                fontFacesCss.Append(vellumBlock).Append('\n');
        }

        var mergedCss = fontFacesCss.Length > 0 ? fontFacesCss + "\n" + css : css.ToString();
        return (mergedCss, fontsToWrite);
    }

    private static List<(string Selector, string Declarations)> ParseStyleRules(CssParser parser, string css)
    {
        var results = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(css))
            return results;

        var sheet = parser.ParseStyleSheet(css);
        foreach (var rule in sheet.Rules.OfType<ICssStyleRule>())
        {
            var selector = NormalizeSelector(rule.SelectorText);
            var declarations = rule.Style.CssText.Trim();
            if (selector.Length > 0 && declarations.Length > 0)
                results.Add((selector, declarations));
        }

        return results;
    }

    private static string NormalizeSelector(string selectorText) =>
        string.Join(", ", selectorText.Split(',').Select(s => s.Trim())).Trim();

    private static Dictionary<string, string> ExtractFontFaceBlocksByFamily(string css)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match blockMatch in FontFaceBlockRegex().Matches(css))
        {
            var block = blockMatch.Value;
            var familyMatch = FontFamilyDeclarationRegex().Match(block);
            if (!familyMatch.Success)
                continue;

            var family = familyMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (family.Length > 0)
                result[family] = block;
        }

        return result;
    }

    private static string BuildFontFaceBlock(string fontFamily, string fileName) =>
        $$"""
        @font-face {
            font-family: "{{fontFamily}}";
            font-weight: normal;
            font-style: normal;
            src: url('fonts/{{fileName}}');
        }
        """;
}

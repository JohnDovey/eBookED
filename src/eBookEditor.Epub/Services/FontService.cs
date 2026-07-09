using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

public record FontFaceReference(string FontFamily, string FileName);

/// <summary>
/// Resolves @font-face declarations in a template stylesheet against font files shipped in
/// the app's "fonts" directory (a sibling of "templates", seeded from the reference EPUB the
/// "Vellum Serif" template was adapted from). Used both to embed the right font files when
/// exporting an EPUB and to install them on the host system when a template is selected.
/// </summary>
public partial class FontService
{
    [GeneratedRegex(@"@font-face\s*\{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex FontFaceBlockRegex();

    [GeneratedRegex("""font-family\s*:\s*["']?([^;"']+)["']?\s*;""")]
    private static partial Regex FontFamilyRegex();

    [GeneratedRegex("""url\(\s*['"]?([^'")]+)['"]?\s*\)""")]
    private static partial Regex SrcUrlRegex();

    private readonly string _fontsDirectory;

    public FontService(string? fontsDirectory = null)
    {
        _fontsDirectory = fontsDirectory ?? Path.Combine(AppContext.BaseDirectory, "fonts");
    }

    public string FontsDirectory => _fontsDirectory;

    /// <summary>Every @font-face rule found in the given CSS, in source order.</summary>
    public IReadOnlyList<FontFaceReference> ParseFontFaces(string css)
    {
        var results = new List<FontFaceReference>();

        foreach (Match block in FontFaceBlockRegex().Matches(css))
        {
            var body = block.Groups[1].Value;
            var familyMatch = FontFamilyRegex().Match(body);
            var urlMatch = SrcUrlRegex().Match(body);
            if (!familyMatch.Success || !urlMatch.Success)
                continue;

            var family = familyMatch.Groups[1].Value.Trim();
            var fileName = Path.GetFileName(urlMatch.Groups[1].Value.Trim());
            results.Add(new FontFaceReference(family, fileName));
        }

        return results;
    }

    /// <summary>Full path to a shipped font file by name, or null if it isn't bundled.</summary>
    public string? ResolveFontFilePath(string fileName)
    {
        var path = Path.Combine(_fontsDirectory, fileName);
        return File.Exists(path) ? path : null;
    }
}

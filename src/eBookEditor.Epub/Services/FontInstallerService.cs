using eBookEditor.Core.Services;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Installs the fonts a template's @font-face rules require onto the host system, so
/// non-EPUB renderers (the app's own editor preview, other apps) can find them by family
/// name too. "Already installed" is approximated by checking whether a same-named font
/// file already exists in the per-user font directory this service would install into —
/// a real cross-platform "is this family name registered" check would need a different
/// native API per OS (CoreText/DirectWrite/Fontconfig), which is out of proportion to what
/// this app needs: the exact file it would copy is the thing worth not re-copying.
/// </summary>
public class FontInstallerService
{
    private readonly FontService _fontService;
    private readonly IFontInstallDirectoryProvider _directoryProvider;

    public FontInstallerService(FontService? fontService = null, IFontInstallDirectoryProvider? directoryProvider = null)
    {
        _fontService = fontService ?? new FontService();
        _directoryProvider = directoryProvider ?? new FontInstallDirectoryProvider();
    }

    /// <summary>
    /// Installs any fonts referenced by the given CSS's @font-face rules that ship with the
    /// app but aren't yet present in the host's user font directory. Returns the family
    /// names actually installed (empty if everything was already present, or if this OS has
    /// no known per-user font directory).
    /// </summary>
    public IReadOnlyList<string> EnsureFontsInstalled(string templateCss)
    {
        var installed = new List<string>();

        var userFontsDir = _directoryProvider.UserFontsDirectory;
        if (string.IsNullOrEmpty(userFontsDir))
            return installed;

        foreach (var font in _fontService.ParseFontFaces(templateCss))
        {
            var sourcePath = _fontService.ResolveFontFilePath(font.FileName);
            if (sourcePath is null)
                continue;

            var destPath = Path.Combine(userFontsDir, Path.GetFileName(sourcePath));
            if (File.Exists(destPath))
                continue;

            Directory.CreateDirectory(userFontsDir);
            File.Copy(sourcePath, destPath, overwrite: false);
            installed.Add(font.FontFamily);
        }

        return installed;
    }
}

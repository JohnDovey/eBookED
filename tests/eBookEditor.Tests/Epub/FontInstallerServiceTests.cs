using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.Epub;

public class FontInstallerServiceTests : IDisposable
{
    private readonly string _shippedFontsDir;
    private readonly string _userFontsDir;
    private readonly FontInstallerService _installer;

    public FontInstallerServiceTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        _shippedFontsDir = Path.Combine(root, "shipped-fonts");
        _userFontsDir = Path.Combine(root, "user-fonts");
        Directory.CreateDirectory(_shippedFontsDir);
        File.WriteAllText(Path.Combine(_shippedFontsDir, "Alegreya-Regular.ttf"), "fake font bytes");

        _installer = new FontInstallerService(
            new FontService(_shippedFontsDir),
            new StubFontInstallDirectoryProvider(_userFontsDir));
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_shippedFontsDir)!;
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private const string CssWithOneFontFace = """
        @font-face {
            font-family: Alegreya;
            src: url('fonts/Alegreya-Regular.ttf');
        }
        body { font-family: Alegreya, serif; }
        """;

    [Fact]
    public void EnsureFontsInstalled_CopiesShippedFontIntoUserFontsDirectory()
    {
        var installed = _installer.EnsureFontsInstalled(CssWithOneFontFace);

        Assert.Equal(["Alegreya"], installed);
        Assert.True(File.Exists(Path.Combine(_userFontsDir, "Alegreya-Regular.ttf")));
    }

    [Fact]
    public void EnsureFontsInstalled_SkipsFontsAlreadyPresentInUserFontsDirectory()
    {
        Directory.CreateDirectory(_userFontsDir);
        File.WriteAllText(Path.Combine(_userFontsDir, "Alegreya-Regular.ttf"), "already installed");

        var installed = _installer.EnsureFontsInstalled(CssWithOneFontFace);

        Assert.Empty(installed);
    }

    [Fact]
    public void EnsureFontsInstalled_SkipsFontsNotShippedWithTheApp()
    {
        const string css = """
            @font-face {
                font-family: SomeOtherFont;
                src: url('fonts/SomeOtherFont.ttf');
            }
            """;

        var installed = _installer.EnsureFontsInstalled(css);

        Assert.Empty(installed);
        Assert.False(Directory.Exists(_userFontsDir));
    }

    [Fact]
    public void EnsureFontsInstalled_ReturnsEmptyWhenNoUserFontsDirectoryIsKnown()
    {
        var installer = new FontInstallerService(
            new FontService(_shippedFontsDir),
            new StubFontInstallDirectoryProvider(null));

        var installed = installer.EnsureFontsInstalled(CssWithOneFontFace);

        Assert.Empty(installed);
    }

    private class StubFontInstallDirectoryProvider(string? userFontsDirectory) : IFontInstallDirectoryProvider
    {
        public string? UserFontsDirectory { get; } = userFontsDirectory;
    }
}

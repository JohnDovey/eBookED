using eBookEditor.Epub.Services;
using eBookEditor.Pdf.Services;

namespace eBookEditor.Tests.Pdf;

public class PdfTemplateFontsTests : IDisposable
{
    private readonly string _tempDir;

    public PdfTemplateFontsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RegisterAndResolve_FallsBackToDefaultFontFamily_WhenTemplateHasNoCustomFonts()
    {
        var fonts = new PdfTemplateFonts(new FontService(_tempDir)).RegisterAndResolve(DefaultStylesheet.Css);

        Assert.Equal(PdfTemplateFonts.DefaultFontFamily, fonts.BodyFontFamily);
        Assert.Equal(PdfTemplateFonts.DefaultFontFamily, fonts.HeadingFontFamily);
    }

    [Fact]
    public void RegisterAndResolve_FallsBackToDefaultFontFamily_WhenReferencedFontFileIsMissing()
    {
        const string css = """
            @font-face { font-family: Alegreya; src: url('fonts/Alegreya-Regular.ttf'); }
            body { font-family: Alegreya, serif; }
            """;

        // No font file written to _tempDir, so the @font-face reference can't be resolved.
        var fonts = new PdfTemplateFonts(new FontService(_tempDir)).RegisterAndResolve(css);

        Assert.Equal(PdfTemplateFonts.DefaultFontFamily, fonts.BodyFontFamily);
    }

    [Fact]
    public void RegisterAndResolve_UsesTheRealShippedVellumSerifFonts()
    {
        var repoRoot = FindRepoRoot();
        var fontsDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "fonts");
        var templatesDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "templates");
        var css = File.ReadAllText(Path.Combine(templatesDir, "Vellum Serif.css"));

        var fonts = new PdfTemplateFonts(new FontService(fontsDir)).RegisterAndResolve(css);

        Assert.Equal("Alegreya", fonts.BodyFontFamily);
        Assert.Equal("Cinzel Decorative", fonts.HeadingFontFamily);
    }

    [Fact]
    public void RegisterAndResolve_UsesTheRealShippedRoyalRoadFonts()
    {
        var repoRoot = FindRepoRoot();
        var fontsDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "fonts");
        var templatesDir = Path.Combine(repoRoot, "src", "eBookEditor.App", "templates");
        var css = File.ReadAllText(Path.Combine(templatesDir, "RoyalRoad.css"));

        var fonts = new PdfTemplateFonts(new FontService(fontsDir)).RegisterAndResolve(css);

        Assert.Equal("Open Sans", fonts.BodyFontFamily);
        Assert.Equal("Open Sans", fonts.HeadingFontFamily);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eBookEditor.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test assembly location.");
    }
}

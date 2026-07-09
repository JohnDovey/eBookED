using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.Epub;

public class FontServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FontService _fontService;

    public FontServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _fontService = new FontService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ParseFontFaces_ExtractsFamilyAndFileNameFromEachBlock()
    {
        const string css = """
            @font-face {
                font-family: Alegreya;
                font-weight: normal;
                src: url('fonts/Alegreya-Regular.ttf');
            }
            @font-face {
                font-family: "Great Vibes";
                src: url("fonts/GreatVibes-Regular.ttf");
            }
            body { font-family: Alegreya, serif; }
            """;

        var faces = _fontService.ParseFontFaces(css);

        Assert.Equal(2, faces.Count);
        Assert.Equal("Alegreya", faces[0].FontFamily);
        Assert.Equal("Alegreya-Regular.ttf", faces[0].FileName);
        Assert.Equal("Great Vibes", faces[1].FontFamily);
        Assert.Equal("GreatVibes-Regular.ttf", faces[1].FileName);
    }

    [Fact]
    public void ParseFontFaces_ReturnsEmptyWhenNoFontFaceRules()
    {
        var faces = _fontService.ParseFontFaces("body { color: red; }");

        Assert.Empty(faces);
    }

    [Fact]
    public void ResolveFontFilePath_ReturnsPathWhenFileExists()
    {
        var fontPath = Path.Combine(_tempDir, "Alegreya-Regular.ttf");
        File.WriteAllText(fontPath, "fake font bytes");

        var resolved = _fontService.ResolveFontFilePath("Alegreya-Regular.ttf");

        Assert.Equal(fontPath, resolved);
    }

    [Fact]
    public void ResolveFontFilePath_ReturnsNullWhenFileMissing()
    {
        var resolved = _fontService.ResolveFontFilePath("DoesNotExist.ttf");

        Assert.Null(resolved);
    }
}

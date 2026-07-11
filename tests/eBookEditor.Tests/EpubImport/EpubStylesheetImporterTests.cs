using eBookEditor.EpubImport.Services;

namespace eBookEditor.Tests.EpubImport;

public class EpubStylesheetImporterTests
{
    private const string VellumCss = """
        @font-face {
            font-family: Alegreya;
            font-weight: normal;
            font-style: normal;
            src: url('fonts/Alegreya-Regular.ttf');
        }

        body {
            font-family: Alegreya, serif;
        }

        h1 {
            font-family: Alegreya, serif;
            margin: 0;
        }

        p {
            margin: 0;
            text-align: justify;
        }
        """;

    [Fact]
    public void BuildTemplate_UsesSourceDeclarationsForASelectorTheSourceDefines()
    {
        const string sourceCss = "body { font-family: \"Open Sans\", sans-serif; }";

        var (mergedCss, _) = EpubStylesheetImporter.BuildTemplate([sourceCss], new Dictionary<string, byte[]>(), VellumCss);

        Assert.Contains("Open Sans", mergedCss);
    }

    [Fact]
    public void BuildTemplate_FallsBackToVellumForASelectorTheSourceDoesNotDefine()
    {
        const string sourceCss = "body { font-family: \"Open Sans\", sans-serif; }";

        var (mergedCss, _) = EpubStylesheetImporter.BuildTemplate([sourceCss], new Dictionary<string, byte[]>(), VellumCss);

        // The source never defines "p", so Vellum's own justified-text declaration survives.
        Assert.Contains("text-align: justify", mergedCss);
    }

    [Fact]
    public void BuildTemplate_WithNoSourceCss_FallsBackToVellumEntirely()
    {
        var (mergedCss, fontsToWrite) = EpubStylesheetImporter.BuildTemplate([], new Dictionary<string, byte[]>(), VellumCss);

        Assert.Contains("Alegreya", mergedCss);
        Assert.Contains("text-align: justify", mergedCss);
        Assert.Empty(fontsToWrite);
    }

    [Fact]
    public void BuildTemplate_ExtractsASourceDefinedFontBackingAUsedFamily()
    {
        const string sourceCss = """
            @font-face {
                font-family: "My Custom Font";
                src: url('fonts/custom.ttf');
            }

            body {
                font-family: "My Custom Font", serif;
            }
            """;
        var fontBytes = new byte[] { 1, 2, 3, 4 };
        var sourceFonts = new Dictionary<string, byte[]> { ["custom.ttf"] = fontBytes };

        var (mergedCss, fontsToWrite) = EpubStylesheetImporter.BuildTemplate([sourceCss], sourceFonts, VellumCss);

        Assert.Contains("My Custom Font", mergedCss);
        Assert.Contains("url('fonts/custom.ttf')", mergedCss);
        Assert.Equal(fontBytes, fontsToWrite["custom.ttf"]);
    }

    [Fact]
    public void BuildTemplate_EditorStyleCatalogClassesAlwaysFallBackToVellum()
    {
        // A foreign EPUB's CSS essentially never defines this app's own "Apply Style" classes
        // (drop-cap, verse, etc.) — confirm the merge still includes Vellum's own definitions
        // for them even when the source stylesheet is otherwise fully populated.
        const string sourceCss = "body { font-family: sans-serif; } h1 { font-family: sans-serif; } p { text-align: left; }";
        const string vellumWithDropCap = VellumCss + "\n.drop-cap:first-letter { font-size: 3.2em; }";

        var (mergedCss, _) = EpubStylesheetImporter.BuildTemplate([sourceCss], new Dictionary<string, byte[]>(), vellumWithDropCap);

        Assert.Contains(".drop-cap:first-letter", mergedCss);
        Assert.Contains("font-size: 3.2em", mergedCss);
    }
}

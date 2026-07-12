using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.Epub;

public class TemplateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TemplateService _templateService;

    public TemplateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        _templateService = new TemplateService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ScanTemplateNames_SeedsDefaultTemplateWhenDirectoryIsEmpty()
    {
        var names = _templateService.ScanTemplateNames();

        Assert.Contains("Default", names);
        Assert.True(File.Exists(Path.Combine(_tempDir, "Default.css")));
        Assert.Equal(DefaultStylesheet.Css, File.ReadAllText(Path.Combine(_tempDir, "Default.css")));
    }

    [Fact]
    public void ScanTemplateNames_ReturnsNamesSortedAlphabeticallyDescending()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Alpha.css"), "body{}");
        File.WriteAllText(Path.Combine(_tempDir, "Zebra.css"), "body{}");
        File.WriteAllText(Path.Combine(_tempDir, "Midnight.css"), "body{}");

        var names = _templateService.ScanTemplateNames();

        Assert.Equal(["Zebra", "Midnight", "Default", "Alpha"], names);
    }

    [Fact]
    public void ScanTemplateNames_RescansOnEveryCall()
    {
        _templateService.ScanTemplateNames();
        File.WriteAllText(Path.Combine(_tempDir, "NewOne.css"), "body{}");

        var names = _templateService.ScanTemplateNames();

        Assert.Contains("NewOne", names);
    }

    [Fact]
    public void GetTemplateCss_ReturnsNamedTemplateContent()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Custom.css"), "body { color: red; }");

        var css = _templateService.GetTemplateCss("Custom");

        Assert.Equal("body { color: red; }", css);
    }

    [Fact]
    public void GetTemplateCss_FallsBackToBuiltInStylesheetWhenTemplateMissing()
    {
        var css = _templateService.GetTemplateCss("DoesNotExist");

        Assert.Equal(DefaultStylesheet.Css, css);
    }

    [Fact]
    public void GetTemplateCss_FallsBackToBuiltInStylesheetWhenNameIsNull()
    {
        var css = _templateService.GetTemplateCss(null);

        Assert.Equal(DefaultStylesheet.Css, css);
    }

    [Fact]
    public void EnsureRequiredStylesPresent_AddsMissingClassesWithDefaultDeclarations()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "HandAuthored.css"), "body { color: red; }");

        _templateService.EnsureRequiredStylesPresent("HandAuthored");

        var css = File.ReadAllText(Path.Combine(_tempDir, "HandAuthored.css"));
        Assert.Contains(".centered-block { text-align: center; }", css);
        Assert.Contains(".contributor-name { font-weight: bold; }", css);
        Assert.Contains(".author-name { font-size: 1.3em; }", css);
        Assert.Contains("body { color: red; }", css);
    }

    [Fact]
    public void EnsureRequiredStylesPresent_ClassAlreadyDefined_LeavesItUntouched()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "PartiallyStyled.css"),
            "body { color: red; }\n.centered-block { text-align: center; font-weight: bold; }");

        _templateService.EnsureRequiredStylesPresent("PartiallyStyled");

        var css = File.ReadAllText(Path.Combine(_tempDir, "PartiallyStyled.css"));
        Assert.Contains(".centered-block { text-align: center; font-weight: bold; }", css);
        Assert.DoesNotContain(".centered-block { text-align: center; }\n", css);
        Assert.Contains(".contributor-name { font-weight: bold; }", css);
    }

    [Fact]
    public void EnsureRequiredStylesPresent_AllClassesAlreadyDefined_DoesNotRewriteTheFile()
    {
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "FullyStyled.css");
        File.WriteAllText(path, DefaultStylesheet.Css);
        var originalWriteTime = File.GetLastWriteTimeUtc(path);

        Thread.Sleep(10);
        _templateService.EnsureRequiredStylesPresent("FullyStyled");

        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void EnsureRequiredStylesPresent_UnknownTemplateName_DoesNothing()
    {
        // No throw, no file created — a defensive no-op rather than an error, matching
        // GetTemplateCss's own fallback-not-failure behavior for a missing template.
        _templateService.EnsureRequiredStylesPresent("DoesNotExist");

        Assert.False(File.Exists(Path.Combine(_tempDir, "DoesNotExist.css")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EnsureRequiredStylesPresent_NoTemplateName_DoesNothing(string? name)
    {
        var exception = Record.Exception(() => _templateService.EnsureRequiredStylesPresent(name));

        Assert.Null(exception);
    }
}

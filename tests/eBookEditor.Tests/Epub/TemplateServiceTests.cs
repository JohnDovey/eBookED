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
}

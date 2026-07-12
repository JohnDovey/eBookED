using System.Text.RegularExpressions;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.Core;

public class GeneratedPageStyleCatalogTests
{
    [Fact]
    public void AllCatalogClasses_HaveAMatchingRule_InDefaultStylesheet() =>
        AssertAllClassesStyled(DefaultStylesheet.Css);

    [Theory]
    [InlineData("Vellum Serif")]
    [InlineData("RoyalRoad")]
    public void AllCatalogClasses_HaveAMatchingRule_InEveryShippedTemplate(string templateName)
    {
        // PageGeneratorService's generated pages (Title, Imprint, etc.) must render correctly
        // regardless of which template is selected — otherwise picking a shipped template
        // other than "Default" would silently lose the centering/sizing/bold treatment these
        // classes provide.
        var path = Path.Combine(AppContext.BaseDirectory, "templates", $"{templateName}.css");
        AssertAllClassesStyled(File.ReadAllText(path));
    }

    private static void AssertAllClassesStyled(string css)
    {
        foreach (var style in GeneratedPageStyleCatalog.Styles)
        {
            // A class can be the whole selector (".caption {") or the start of a compound one
            // — just require the class name to appear as a selector token, not glued to a
            // longer class name.
            var pattern = $@"\.{Regex.Escape(style.ClassName)}(?=[\s{{:,])";
            Assert.True(Regex.IsMatch(css, pattern), $"No CSS rule found for .{style.ClassName}");
        }
    }
}

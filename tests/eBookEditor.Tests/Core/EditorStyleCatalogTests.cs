using System.Text.RegularExpressions;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;

namespace eBookEditor.Tests.Core;

public class EditorStyleCatalogTests
{
    [Fact]
    public void AllCatalogClasses_HaveAMatchingRule_InDefaultStylesheet() =>
        AssertAllClassesStyled(DefaultStylesheet.Css);

    [Fact]
    public void AllCatalogClasses_HaveAMatchingRule_InTheShippedVellumSerifTemplate()
    {
        // Every "Apply Style" catalog entry must actually be styled by every shipped
        // template, not just the built-in default — otherwise picking "Vellum Serif" would
        // silently leave some of the menu's own styles with no visible effect in the EPUB.
        var path = Path.Combine(AppContext.BaseDirectory, "templates", "Vellum Serif.css");
        AssertAllClassesStyled(File.ReadAllText(path));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Vellum Serif")]
    public void DropCapAndAttribution_SelectorsApplyDirectlyToTheWrappingElement(string templateName)
    {
        // MainWindow.OnApplyStyleClick wraps a block-style selection as a flat
        // "<div class='...'>{text}</div>" — no nested <p>. A selector like
        // ".drop-cap p:first-letter" or ".attribution p::before" would never match that shape,
        // which is exactly why both effects silently failed to render (confirmed real bug, not
        // a hypothetical one) until the selectors were changed to apply to the div itself.
        var css = templateName == "Default"
            ? DefaultStylesheet.Css
            : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", $"{templateName}.css"));

        Assert.Contains(".drop-cap:first-letter", css);
        Assert.Contains(".attribution::before", css);
        Assert.DoesNotContain(".drop-cap p:first-letter", css);
        Assert.DoesNotContain(".attribution p::before", css);
    }

    private static void AssertAllClassesStyled(string css)
    {
        foreach (var style in EditorStyleCatalog.Styles)
        {
            // A class can be the whole selector (".caption {") or the start of a compound one
            // (".drop-cap:first-letter {", ".attribution::before {") — just require the class
            // name to appear as a selector token, not glued to a longer class name.
            var pattern = $@"\.{Regex.Escape(style.ClassName)}(?=[\s{{:,])";
            Assert.True(Regex.IsMatch(css, pattern), $"No CSS rule found for .{style.ClassName}");
        }
    }
}

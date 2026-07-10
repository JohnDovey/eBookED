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

    private static void AssertAllClassesStyled(string css)
    {
        foreach (var style in EditorStyleCatalog.Styles)
        {
            // A class can be the whole selector (".caption {") or the start of a compound one
            // (".drop-cap p:first-letter {", ".attribution p::before {") — just require the
            // class name to appear as a selector token, not glued to a longer class name.
            var pattern = $@"\.{Regex.Escape(style.ClassName)}(?=[\s{{:,])";
            Assert.True(Regex.IsMatch(css, pattern), $"No CSS rule found for .{style.ClassName}");
        }
    }
}

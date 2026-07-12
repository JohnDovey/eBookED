namespace eBookEditor.Core.Services;

/// <summary>A named CSS class one of PageGeneratorService's auto-generated front/back-matter
/// pages (Title, Imprint, etc.) relies on for formatting, and the default declarations to use
/// for it when a template doesn't already define it.</summary>
public record GeneratedPageStyle(string ClassName, string DefaultDeclarations);

/// <summary>
/// The CSS classes PageGeneratorService's generated pages use in place of hardcoded inline
/// style="..." attributes — centering the Imprint page's title/contributor block and publisher
/// logo, sizing the author's name larger, and marking contributor/publisher names — so every
/// shipped template controls the actual look (a template can freely override a class selector;
/// it can't override an inline style without resorting to !important). Every shipped template
/// must define all of these (see GeneratedPageStyleCatalogTests, mirroring how
/// EditorStyleCatalogTests checks its own classes); TemplateService.EnsureRequiredStylesPresent
/// adds any missing one with its default declarations here to a template (imported or
/// hand-authored) the moment it's selected, rather than leaving it silently unstyled.
/// </summary>
public static class GeneratedPageStyleCatalog
{
    public static readonly IReadOnlyList<GeneratedPageStyle> Styles =
    [
        new("centered-block", "text-align: center;"),
        new("contributor-name", "font-weight: bold;"),
        new("author-name", "font-size: 1.3em;"),
    ];
}

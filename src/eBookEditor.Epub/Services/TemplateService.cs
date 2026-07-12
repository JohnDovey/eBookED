using System.Text;
using System.Text.RegularExpressions;
using eBookEditor.Core.Services;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Manages the "templates" directory of selectable CSS stylesheets, kept alongside the
/// installed app (not per-project) so templates are shared across every book. Seeds a
/// "Default" template from the built-in stylesheet the first time it's needed, so there's
/// always at least one valid selection.
/// </summary>
public class TemplateService
{
    public const string DefaultTemplateName = "Default";

    private readonly string _templatesDirectory;

    public TemplateService(string? templatesDirectory = null)
    {
        _templatesDirectory = templatesDirectory ?? Path.Combine(AppContext.BaseDirectory, "templates");
    }

    public string TemplatesDirectory => _templatesDirectory;

    /// <summary>
    /// Rescans the templates directory (seeding "Default" first if the directory doesn't
    /// exist or is empty) and returns template display names sorted alphabetically descending.
    /// </summary>
    public IReadOnlyList<string> ScanTemplateNames()
    {
        EnsureDefaultTemplateSeeded();

        return Directory.EnumerateFiles(_templatesDirectory, "*.css")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public void EnsureDefaultTemplateSeeded()
    {
        Directory.CreateDirectory(_templatesDirectory);

        var defaultPath = Path.Combine(_templatesDirectory, $"{DefaultTemplateName}.css");
        if (!File.Exists(defaultPath))
            File.WriteAllText(defaultPath, DefaultStylesheet.Css);
    }

    /// <summary>
    /// Returns the CSS content for the named template, falling back to the built-in
    /// stylesheet if no name is given or the template file is missing.
    /// </summary>
    public string GetTemplateCss(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return DefaultStylesheet.Css;

        var path = Path.Combine(_templatesDirectory, $"{templateName}.css");
        return File.Exists(path) ? File.ReadAllText(path) : DefaultStylesheet.Css;
    }

    /// <summary>
    /// Ensures the named template's CSS file defines every class PageGeneratorService's
    /// generated pages need (see GeneratedPageStyleCatalog), appending a rule using each
    /// missing class's own default declarations and persisting the change back to the
    /// template's file. Called whenever a template is selected (see StyleWindow's
    /// OnTemplateSelectionChanged), so a hand-authored or older template someone drops into
    /// the templates directory ends up correctly styled the moment it's picked, instead of
    /// silently missing the centering/sizing/bold treatment a generated page relies on.
    /// A template freshly built by EpubStylesheetImporter never needs this — it always derives
    /// its full selector list from "Vellum Serif.css", which already defines every one of
    /// these classes — but this still covers it defensively, in case that ever changes.
    /// </summary>
    public void EnsureRequiredStylesPresent(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return;

        var path = Path.Combine(_templatesDirectory, $"{templateName}.css");
        if (!File.Exists(path))
            return;

        var css = File.ReadAllText(path);
        var missing = GeneratedPageStyleCatalog.Styles.Where(style => !DefinesClass(css, style.ClassName)).ToList();
        if (missing.Count == 0)
            return;

        var sb = new StringBuilder(css.TrimEnd());
        sb.AppendLine().AppendLine();
        sb.AppendLine("/* Added automatically — required by the app's auto-generated pages. */");
        foreach (var style in missing)
            sb.AppendLine($".{style.ClassName} {{ {style.DefaultDeclarations} }}");

        File.WriteAllText(path, sb.ToString());
    }

    // A class can be the whole selector (".caption {") or the start of a compound one
    // (".author-name::first-letter {") — just require the class name to appear as a selector
    // token, not glued to a longer class name (e.g. ".author-name-large" shouldn't count as
    // already defining ".author-name").
    private static bool DefinesClass(string css, string className) =>
        Regex.IsMatch(css, $@"\.{Regex.Escape(className)}(?=[\s{{:,])");
}

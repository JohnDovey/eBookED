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
}

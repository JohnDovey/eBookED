using System.Net;
using System.Reflection;

namespace eBookEditor.Html.Services;

/// <summary>
/// Builds the "Created with eBook Editor X.Y.Z on yyyy-MM-dd HH:mm" credit line shown on the
/// imprint page, just above the copyright statement — computed fresh at the moment each export
/// actually runs (not baked into the stored copyright.ebhtml at front-matter-regeneration time),
/// so it reflects when that specific EPUB/PDF/DOCX file was produced, not whenever metadata was
/// last edited; a project can go through many exports between metadata edits, and a frozen
/// "created on" date from the last regeneration would go stale across every one of them. The
/// version comes from whichever assembly calls Build() — every project in this solution shares
/// one &lt;Version&gt; via Directory.Build.props, so it's the same number regardless of which
/// project's assembly is asked. generatedAt is a parameter rather than read internally
/// (DateTime.Now) so callers — and tests — control exactly what timestamp appears.
/// </summary>
public static class GeneratedByLine
{
    private const string AppName = "eBook Editor";
    private const string CopyrightStatementMarker = "<p>Copyright ©";

    public static string Build(DateTime generatedAt)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null ? "" : $" {version.ToString(3)}";
        return $"Created with {AppName}{versionText} on {generatedAt:yyyy-MM-dd HH:mm}";
    }

    /// <summary>
    /// Inserts the credit line as its own paragraph immediately before the imprint page's
    /// "Copyright ©" paragraph (the exact shape PageGeneratorService.GenerateCopyrightPage
    /// always produces). If a hand-edited imprint page doesn't contain that marker, the HTML is
    /// returned unchanged rather than guessing where to inject — silently corrupting a
    /// hand-authored page would be worse than the credit line simply not appearing on it.
    /// </summary>
    public static string InsertBeforeCopyrightStatement(string copyrightPageHtml, DateTime generatedAt)
    {
        var index = copyrightPageHtml.IndexOf(CopyrightStatementMarker, StringComparison.Ordinal);
        if (index < 0)
            return copyrightPageHtml;

        var creditParagraph = $"<p><em>{WebUtility.HtmlEncode(Build(generatedAt))}</em></p>\n";
        return copyrightPageHtml[..index] + creditParagraph + copyrightPageHtml[index..];
    }
}

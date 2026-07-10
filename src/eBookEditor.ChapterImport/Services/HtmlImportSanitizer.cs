using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace eBookEditor.ChapterImport.Services;

/// <summary>
/// Turns an arbitrary imported .html/.htm file (e.g. exported from Google Docs, a saved web
/// page, or another authoring tool) into a clean HTML fragment safe to store as a chapter
/// body. This is a sanitizing pass-through, not a format conversion, since the target format
/// (HTML) is already what the source file contains: strips &lt;script&gt;/&lt;style&gt;
/// elements and "head" content entirely, and strips "javascript:" URLs and inline "on*" event
/// handler attributes — this app's own generated pages carry their own styling, and the
/// WYSIWYG editor renders imported content in a real WebView, so arbitrary embedded script
/// must never survive import. Everything else is kept as-is.
/// </summary>
public class HtmlImportSanitizer
{
    private readonly HtmlParser _parser = new();

    public string Convert(string html)
    {
        var document = _parser.ParseDocument(html);
        var root = document.Body ?? document.DocumentElement;

        foreach (var element in root.QuerySelectorAll("script, style").ToList())
            element.Remove();

        SanitizeAttributes(root);

        return root.InnerHtml.Trim();
    }

    private static void SanitizeAttributes(IElement root)
    {
        foreach (var element in root.QuerySelectorAll("*").Prepend(root))
        {
            foreach (var attribute in element.Attributes.ToList())
            {
                var isEventHandler = attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase);
                var isScriptUrl = attribute.Name is "href" or "src" &&
                    attribute.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);

                if (isEventHandler || isScriptUrl)
                    element.RemoveAttribute(attribute.Name);
            }
        }
    }
}

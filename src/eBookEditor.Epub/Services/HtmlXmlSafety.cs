using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Markdig's HTML output is well-formed HTML5, not guaranteed well-formed XML: void elements
/// (img/br/hr) aren't always self-closed, and named HTML entities beyond the five XML
/// predefines (e.g. &amp;nbsp;) aren't valid standalone XML. Content docs must be strict
/// XHTML, so this normalizes both before the fragment is parsed as XML.
/// </summary>
internal static partial class HtmlXmlSafety
{
    [GeneratedRegex(@"<(img|br|hr)\b([^>/]*)(?<!/)>", RegexOptions.IgnoreCase)]
    private static partial Regex VoidElementRegex();

    private static readonly Dictionary<string, string> NamedEntityReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["&nbsp;"] = "&#160;",
        ["&mdash;"] = "&#8212;",
        ["&ndash;"] = "&#8211;",
        ["&ldquo;"] = "&#8220;",
        ["&rdquo;"] = "&#8221;",
        ["&lsquo;"] = "&#8216;",
        ["&rsquo;"] = "&#8217;",
        ["&hellip;"] = "&#8230;"
    };

    public static string MakeXmlSafe(string html)
    {
        var withClosedVoids = VoidElementRegex().Replace(html, m => $"<{m.Groups[1].Value}{m.Groups[2].Value} />");

        foreach (var (named, numeric) in NamedEntityReplacements)
            withClosedVoids = withClosedVoids.Replace(named, numeric, StringComparison.OrdinalIgnoreCase);

        return withClosedVoids;
    }
}

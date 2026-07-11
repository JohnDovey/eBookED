using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// HTML5 void elements (img/br/hr) aren't always self-closed, named HTML entities beyond the
/// five XML predefines (e.g. &amp;nbsp;) aren't valid standalone XML, and a bare "&amp;" not
/// starting a real entity reference (most commonly an unescaped query string in a stored
/// href, e.g. "?a=1&amp;b=2" typed or pasted by a user rather than produced by this app's own
/// WebUtility.HtmlEncode calls) throws the XML parser entirely. Content docs must be strict
/// XHTML, so this normalizes all three before the fragment is parsed as XML.
/// </summary>
internal static partial class HtmlXmlSafety
{
    // The attribute-content group must allow "/" (e.g. an unencoded "src="../images/x.jpg""
    // path) — excluding it was a latent bug that never manifested while every &lt;img&gt; came
    // from Markdig's own self-closing HTML renderer; real stored HTML from this app's own
    // generators/converters routinely has slashes in attribute values. The trailing negative
    // lookbehind alone is what correctly skips tags already self-closed with " />".
    [GeneratedRegex(@"<(img|br|hr)\b([^>]*)(?<!/)>", RegexOptions.IgnoreCase)]
    private static partial Regex VoidElementRegex();

    // Matches "&" only when it does NOT already start something that looks like a real entity
    // reference (a named reference ending in ";", or a numeric/hex character reference ending
    // in ";") — so a genuine "&amp;"/"&#39;"/"&nbsp;" is left untouched, while a raw "&" from
    // hand-typed or pasted content (most often a query-string "&" in an href, which HTML
    // tolerates but XML does not) gets escaped instead of crashing the parser.
    [GeneratedRegex(@"&(?![a-zA-Z][a-zA-Z0-9]*;|#[0-9]+;|#x[0-9a-fA-F]+;)")]
    private static partial Regex BareAmpersandRegex();

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
        var withEscapedAmpersands = BareAmpersandRegex().Replace(withClosedVoids, "&amp;");

        foreach (var (named, numeric) in NamedEntityReplacements)
            withEscapedAmpersands = withEscapedAmpersands.Replace(named, numeric, StringComparison.OrdinalIgnoreCase);

        return withEscapedAmpersands;
    }
}

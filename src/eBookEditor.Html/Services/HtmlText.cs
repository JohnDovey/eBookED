using AngleSharp.Html.Parser;

namespace eBookEditor.Html.Services;

/// <summary>Plain-text operations on a stored HTML chapter body — used where callers need to
/// treat the body as prose (word counts for the post-export result summary) rather than markup.
/// A naive whitespace split on the raw HTML text would count "&lt;p&gt;Hello" as one token
/// instead of "Hello" alone (no space between a tag and adjacent text), inflating or deflating
/// the count depending on how tags happen to bump up against words.</summary>
public static class HtmlText
{
    private static readonly HtmlParser Parser = new();

    public static int CountWords(string html)
    {
        var document = Parser.ParseDocument($"<body>{html}</body>");
        var text = document.Body?.TextContent ?? "";
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

namespace eBookEditor.Html.Services;

/// <summary>
/// Parses an HTML body fragment plus a CSS stylesheet into a real, cascade-aware DOM — used by
/// the PDF and Word exporters so EditorStyleCatalog classes (and any other CSS a template
/// defines) actually affect rendering instead of being structurally present but invisible.
/// AngleSharp.Css resolves specificity and inheritance for real; each computed property still
/// comes back as the CSS text that won the cascade (e.g. "3em", "90%", "bold"), not resolved to
/// an absolute unit — see CssValueParser for turning that into a concrete point size against
/// whatever base font size the caller is rendering at.
///
/// A small user-agent baseline stylesheet is prepended ahead of the template CSS so ordinary
/// tag semantics (&lt;strong&gt; is bold, &lt;em&gt; is italic, etc.) work the way every real
/// browser's built-in stylesheet provides — AngleSharp.Css only resolves what's explicitly
/// declared, it has no built-in default stylesheet of its own. The template CSS is cascaded on
/// top, so it can still override any of these defaults, same as an author stylesheet would.
/// </summary>
public sealed class HtmlStyleDocument
{
    private const string UserAgentBaselineCss = """
        strong, b { font-weight: bold; }
        em, i, dfn, var, cite { font-style: italic; }
        u, ins { text-decoration: underline; }
        s, strike, del { text-decoration: line-through; }
        mark { background-color: yellow; }
        code, kbd, samp, pre { font-family: monospace; }
        sub { vertical-align: sub; font-size: 75%; }
        sup { vertical-align: super; font-size: 75%; }
        """;

    private readonly IDocument _document;
    private readonly IWindow _window;

    private HtmlStyleDocument(IDocument document)
    {
        _document = document;
        _window = document.DefaultView!;
    }

    public static HtmlStyleDocument Parse(string bodyHtml, string? templateCss)
    {
        var css = UserAgentBaselineCss + "\n" + (templateCss ?? "");
        var fullHtml = $"<!DOCTYPE html><html><head><style>{css}</style></head><body>{bodyHtml}</body></html>";

        var configuration = Configuration.Default.WithCss();
        var context = BrowsingContext.New(configuration);
        // No network I/O happens for string content, so the async API resolves synchronously —
        // safe to block on, and keeps this type's API symmetrical with the (synchronous)
        // renderers that consume it.
        var document = context.OpenAsync(request => request.Content(fullHtml)).GetAwaiter().GetResult();

        return new HtmlStyleDocument(document);
    }

    public IElement Body => _document.Body!;

    public ICssStyleDeclaration ComputedStyle(IElement element) => _window.GetComputedStyle(element);
}

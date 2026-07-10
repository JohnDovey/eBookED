namespace eBookEditor.Core.Services;

/// <summary>A named text style selectable from the editor's right-click "Apply Style" menu,
/// the CSS class name it hooks to, and whether applying it wraps the selection in a
/// &lt;span&gt; (inline — safe mid-paragraph, e.g. "small caps a word") or a &lt;div&gt;
/// (block — its own paragraph-level element, e.g. a verse stanza). Lives in Core (no UI
/// dependency) so the editor UI can list them without needing to know anything about CSS/HTML
/// itself.</summary>
public record EditorStyle(string Label, string ClassName, bool IsBlock);

/// <summary>
/// The curated set of text styles offered from the editor's selection context menu.
/// Adapted from a Vellum-generated reference EPUB's stylesheet (its own ~190 CSS classes are
/// almost entirely Vellum's internal layout plumbing, tied to a much more heavily-nested DOM
/// structure than this app's plain semantic HTML output — these eleven are the genuinely
/// reusable, author-facing text/paragraph styles among them, with simplified rules). Applying
/// one wraps the selected text in real HTML — a &lt;span class="…"&gt; for inline styles, a
/// &lt;div class="…"&gt; for block styles — whose class matches a rule already shipped in
/// DefaultStylesheet.cs and "Vellum Serif.css"; a custom user template that doesn't define
/// these classes just renders the wrapped text unstyled, no different from an unknown HTML
/// class normally.
/// </summary>
public static class EditorStyleCatalog
{
    public static readonly IReadOnlyList<EditorStyle> Styles =
    [
        new("Small Caps", "smallcaps", IsBlock: false),
        new("Underline", "underline", IsBlock: false),
        new("Strikethrough", "strikethrough", IsBlock: false),
        new("Monospace", "monospace", IsBlock: false),
        new("Sans-serif", "sans-serif", IsBlock: false),
        new("All Caps", "all-caps", IsBlock: false),
        new("Verse", "verse", IsBlock: true),
        new("Inset", "inset", IsBlock: true),
        new("Attribution", "attribution", IsBlock: true),
        new("Drop Cap", "drop-cap", IsBlock: true),
        new("Caption", "caption", IsBlock: true),
    ];
}

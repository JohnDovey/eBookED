namespace eBookEditor.Core.Services;

/// <summary>A named text style selectable from the editor's right-click "Apply Style" menu,
/// and the CSS class name it hooks to. Lives in Core (no UI dependency) so the editor UI can
/// list them without needing to know anything about CSS/Markdown itself.</summary>
public record EditorStyle(string Label, string ClassName);

/// <summary>
/// The curated set of text styles offered from the editor's selection context menu.
/// Adapted from a Vellum-generated reference EPUB's stylesheet (its own ~190 CSS classes are
/// almost entirely Vellum's internal layout plumbing, tied to a much more heavily-nested DOM
/// structure than this app's plain semantic HTML output — these ten are the genuinely
/// reusable, author-facing text/paragraph styles among them, with simplified rules). Applying
/// one wraps the selected text in a Markdown custom container — e.g. "::: {.smallcaps} ...
/// :::" — whose class matches a rule already shipped in DefaultStylesheet.cs and "Vellum
/// Serif.css"; a custom user template that doesn't define these classes just renders the
/// wrapped text unstyled, no different from an unknown HTML class normally.
/// </summary>
public static class EditorStyleCatalog
{
    public static readonly IReadOnlyList<EditorStyle> Styles =
    [
        new("Small Caps", "smallcaps"),
        new("Underline", "underline"),
        new("Strikethrough", "strikethrough"),
        new("Monospace", "monospace"),
        new("Sans-serif", "sans-serif"),
        new("All Caps", "all-caps"),
        new("Verse", "verse"),
        new("Inset", "inset"),
        new("Attribution", "attribution"),
        new("Drop Cap", "drop-cap"),
        new("Caption", "caption"),
    ];
}

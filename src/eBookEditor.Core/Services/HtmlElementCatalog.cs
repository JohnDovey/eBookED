namespace eBookEditor.Core.Services;

/// <summary>A basic HTML block element selectable from the editor's "Insert Element" toolbar
/// menu: the tag to wrap the selection in, and placeholder text to use instead when nothing
/// is selected, so e.g. clicking "Heading 1" with no selection still inserts a real, editable
/// heading rather than silently doing nothing. Lives in Core (no UI dependency), mirroring
/// EditorStyleCatalog's shape.</summary>
public record HtmlElementOption(string Label, string Tag, string PlaceholderText);

/// <summary>
/// The curated set of plain (unstyled, no CSS class) HTML elements offered from the editor's
/// "Insert Element" toolbar menu — the semantic block tags every basic HTML editor exposes.
/// Unlike EditorStyleCatalog's entries, these wrap the selection with no class attribute at
/// all, so they render using each template's own default element styling rather than a
/// specific named style.
/// </summary>
public static class HtmlElementCatalog
{
    public static readonly IReadOnlyList<HtmlElementOption> Elements =
    [
        new("Heading 1", "h1", "Heading"),
        new("Heading 2", "h2", "Heading"),
        new("Heading 3", "h3", "Heading"),
        new("Heading 4", "h4", "Heading"),
        new("Paragraph", "p", "Paragraph text"),
        new("Blockquote", "blockquote", "Quoted text"),
    ];
}

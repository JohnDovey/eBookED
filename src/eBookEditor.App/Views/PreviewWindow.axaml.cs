using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using eBookEditor.Markdown.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// A standalone rendered-Markdown preview of the chapter currently open in a MainWindow's
/// editor, kept in sync (content and, approximately, scroll position) as the source is edited —
/// see MainWindow.OnOpenPreviewClick/OnEditorCaretPositionChanged. Living in its own window
/// rather than a toggled pane in the main editor sidesteps a whole class of AvaloniaEdit bug
/// this app kept hitting where a hidden (IsVisible=False) editor's cached visual lines went
/// stale and came back blank when shown again — the editor is now never hidden at all.
/// </summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
    }

    public void UpdateContent(string markdown, string? title)
    {
        // Markdown.Avalonia understands neither custom containers nor attribute blocks (see
        // PreviewMarkdownSanitizer) — feeding it those verbatim shows broken/literal syntax
        // instead of just rendering plainly, so strip them before handing off to it.
        MarkdownViewer.Markdown = PreviewMarkdownSanitizer.Sanitize(markdown);
        Title = title is { Length: > 0 } ? $"Preview — {title}" : "Preview";
    }

    /// <summary>
    /// Scrolls to roughly the same relative position as the cursor in the source editor — a
    /// proportional (line-fraction-based) sync, not an exact block-to-source mapping, since
    /// Markdown.Avalonia doesn't expose a way to resolve a rendered block back to the source
    /// line it came from. Deferred to a later UI-thread turn so the preview's internal
    /// ScrollViewer has actually measured its content (Extent/Viewport) after a content update,
    /// rather than reading stale/zero values from before the new Markdown was laid out.
    /// </summary>
    public void ScrollToFraction(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);

        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = MarkdownViewer.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer is null)
                return;

            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, maxY * fraction);
        }, DispatcherPriority.Loaded);
    }
}

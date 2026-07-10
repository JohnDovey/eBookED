using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// A standalone rendered-HTML preview of the chapter currently open in a MainWindow's editor,
/// kept in sync (content and, approximately, scroll position) as the source is edited — see
/// MainWindow.OnOpenPreviewClick/OnEditorCaretPositionChanged. Renders via a real browser engine
/// (NativeWebView) using the project's actual selected CSS template — HtmlStyleDocument-grade
/// cascade resolution for free, unlike the old Markdown.Avalonia-based preview, which had no CSS
/// engine of its own at all.
/// </summary>
public partial class PreviewWindow : Window
{
    private NativeWebView? _webView;
    private string _pendingCss = string.Empty;
    private string _pendingBody = string.Empty;
    private bool _navigated;
    private double? _pendingScrollFraction;

    public PreviewWindow()
    {
        InitializeComponent();

        // See the Phase 0 WebView spike: attaching a NativeWebView to a Window before that
        // window completes a real Avalonia layout pass crashes natively inside WKWebView's own
        // init ("Invalid view geometry: y is NaN"). Deferring construction/attachment to after
        // the first Loaded-priority dispatch avoids it — this window is freshly constructed each
        // time it's (re)opened, so it always needs this, unlike a WebView embedded in the
        // already-shown MainWindow.
        Dispatcher.UIThread.Post(() =>
        {
            _webView = new NativeWebView();
            ViewerHost.Content = _webView;
            _webView.NavigationCompleted += OnNavigationCompleted;
            Navigate();
        }, DispatcherPriority.Loaded);
    }

    public void UpdateContent(string css, string bodyHtml, string? title)
    {
        _pendingCss = css;
        _pendingBody = bodyHtml;
        Title = title is { Length: > 0 } ? $"Preview — {title}" : "Preview";
        Navigate();
    }

    private void Navigate()
    {
        if (_webView is null)
            return;

        _navigated = false;
        var html = HtmlPageShell.Wrap(_pendingCss, _pendingBody, editable: false);
        _webView.NavigateToString(html, new Uri("about:blank"));
    }

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        _navigated = e.IsSuccess;
        if (e.IsSuccess && _pendingScrollFraction is { } fraction)
        {
            _pendingScrollFraction = null;
            await TryScrollToFraction(fraction);
        }
    }

    /// <summary>
    /// Scrolls to roughly the same relative position as the cursor in the source editor — a
    /// proportional (scroll-height-fraction) sync, not an exact block-to-source mapping. Since
    /// every content update re-navigates the page (simplest way to keep it always in sync with a
    /// read-only view), the fraction is queued if a navigation is still in flight and applied
    /// once NavigationCompleted fires for that page, rather than targeting a page that's about
    /// to be replaced.
    /// </summary>
    public void ScrollToFraction(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        _pendingScrollFraction = fraction;
        if (_navigated)
        {
            _pendingScrollFraction = null;
            _ = TryScrollToFraction(fraction);
        }
    }

    private async Task TryScrollToFraction(double fraction)
    {
        if (_webView is null)
            return;

        try
        {
            await _webView.InvokeScript(
                $"window.ebookEditor.scrollToFraction({fraction.ToString(CultureInfo.InvariantCulture)})");
        }
        catch
        {
            // Best-effort — a call that races a fresh navigation can legitimately fail; scroll
            // sync just skips this one update rather than surfacing an error to the user.
        }
    }
}

using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using eBookEditor.App.Services;
using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

public partial class MainWindow : Window
{
    private const string SpineItemIdFormat = "application/x-ebookeditor-spine-item-id";
    private const double DragThreshold = 6;

    private bool _suppressTextChanged;
    private bool _suppressWysiwygPush;
    private bool _forceClose;
    private SpineItem? _dragCandidate;
    private Point _dragStartPoint;
    private PreviewWindow? _previewWindow;
    private NativeWebView? _wysiwygWebView;
    private bool _wysiwygNavigated;
    private readonly ChapterFileService _chapterFileService = new();
    private readonly DocumentLinkDestinationScanner _linkDestinationScanner = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;
        EditorTextBox.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        // Chapters are HTML now (see the HTML content-model refactor); AvaloniaEdit ships an
        // "HTML" highlighting definition built in, no custom .xshd needed.
        EditorTextBox.SyntaxHighlighting = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("HTML");
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        KeyDown += OnWindowKeyDown;
    }

    /// <summary>
    /// Best-effort Cut/Copy/Paste/Delete shortcuts for WYSIWYG mode, which — unlike raw mode's
    /// AvaloniaEdit (already fully keyboard-native) — has nothing else wired up for this: no
    /// Window.KeyBindings entry works here since none of these are backed by an ICommand (they're
    /// code-behind handlers, matching every other WYSIWYG toolbar command in this app), so this
    /// listens on the window's own KeyDown instead. Skips entirely outside WYSIWYG mode (raw mode
    /// needs nothing extra) and whenever a TextBox/ListBox/the raw editor already has focus (the
    /// chapter title/subtitle fields, the spine list), so this never steals a keystroke a more
    /// specific control already owns. Whether this actually fires while the embedded native
    /// WebView has real OS-level keyboard focus depends on whether that focus is even visible to
    /// Avalonia's routed-event system — the toolbar's Cut/Copy/Paste/Delete buttons are the
    /// mechanism guaranteed to work regardless.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsWysiwygMode || e.Handled)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or ListBox or AvaloniaEdit.TextEditor)
            return;

        var ctrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (ctrlOrCmd && e.Key == Key.X)
        {
            OnWysiwygOrRawCutClick(sender, e);
            e.Handled = true;
        }
        else if (ctrlOrCmd && e.Key == Key.C)
        {
            OnWysiwygOrRawCopyClick(sender, e);
            e.Handled = true;
        }
        else if (ctrlOrCmd && e.Key == Key.V)
        {
            OnWysiwygOrRawPasteClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back)
        {
            OnWysiwygOrRawDeleteClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e) => ViewModel?.RecordProjectClosed();

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || ViewModel?.Editor.IsDirty != true)
            return;

        e.Cancel = true;

        var dialog = new UnsavedChangesDialog();
        var result = await dialog.ShowDialog<UnsavedChangesResult>(this);

        switch (result)
        {
            case UnsavedChangesResult.Save:
                ViewModel.SaveProjectCommand.Execute(null);
                _forceClose = true;
                Close();
                break;
            case UnsavedChangesResult.Discard:
                _forceClose = true;
                Close();
                break;
            case UnsavedChangesResult.Cancel:
                break;
        }
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.Editor.PropertyChanged += OnEditorViewModelPropertyChanged;
        ViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
        SyncEditorTextFromViewModel();
    }

    private async void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.LastExportResult) || ViewModel?.LastExportResult is not { } result)
            return;

        ViewModel.LastExportResult = null;
        await new GenerationResultWindow(result).ShowDialog(this);
    }

    private void OnEditorViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.CurrentText) && !_suppressTextChanged)
            SyncEditorTextFromViewModel();
    }

    /// <summary>
    /// Pushes CurrentText into whichever editor pane isn't its own source (and, if open, the
    /// Preview window). Skipping the AvaloniaEdit reassignment when its text already matches is
    /// essential, not just an optimization: every keystroke round-trips through
    /// OnEditorTextBoxTextChanged -> ViewModel.CurrentText -> this method, and unconditionally
    /// setting TextEditor.Text (even to its own current value) resets the caret/selection on
    /// every character typed, which made typing into the editor effectively impossible. The
    /// WYSIWYG push has the same "don't echo my own edit back to me" concern, guarded by
    /// _suppressWysiwygPush instead (see OnWysiwygWebMessageReceived) since a WebView has no
    /// equivalent "already matches" short-circuit to check against.
    /// </summary>
    private void SyncEditorTextFromViewModel()
    {
        if (ViewModel is null)
            return;

        var text = ViewModel.Editor.CurrentText;

        if (EditorTextBox.Text != text)
        {
            _suppressTextChanged = true;
            EditorTextBox.Text = text;
            _suppressTextChanged = false;
        }

        if (IsWysiwygMode && !_suppressWysiwygPush)
            PushContentToWysiwyg(CurrentBodyOnly());

        if (ViewModel.Editor.FilePath is { } path)
            UpdatePreviewWindow(CurrentBodyOnly(), Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>
    /// CurrentText is the raw full file (front matter YAML block included — see
    /// EditorViewModel.LoadFile/Save) since the raw editor pane is meant to show/edit it
    /// directly. Preview and Rich Text mode should only ever show the rendered body, not the
    /// literal "---\ntitle: ...\n---" block above it — a real bug caught by a user screenshot
    /// showing that block as visible text at the top of a Preview window.
    /// </summary>
    private string CurrentBodyOnly() =>
        ViewModel is null ? string.Empty : _chapterFileService.ParseChapter(ViewModel.Editor.CurrentText).Body;

    /// <summary>
    /// Opens a standalone preview window for the chapter currently in the editor, or brings the
    /// existing one to the front if already open — one preview window per main window, reused
    /// rather than piling up duplicates. Its content and scroll position are then kept in sync
    /// by SyncEditorTextFromViewModel/OnEditorCaretPositionChanged as long as it stays open.
    /// </summary>
    private void OnOpenPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (_previewWindow is null)
        {
            _previewWindow = new PreviewWindow();
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.Show(this);
        }
        else
        {
            _previewWindow.Activate();
        }

        var title = ViewModel.Editor.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : null;
        var body = CurrentBodyOnly();
        _previewWindow.UpdateContent(ViewModel.GetCurrentTemplateCss(), body, title, ChapterHeadingHtmlFor(body), CurrentProjectRootDirectory);
        ScrollPreviewToCaret();
    }

    private void UpdatePreviewWindow(string bodyHtml, string? title)
    {
        if (_previewWindow is null || ViewModel is null)
            return;

        _previewWindow.UpdateContent(ViewModel.GetCurrentTemplateCss(), bodyHtml, title, ChapterHeadingHtmlFor(bodyHtml), CurrentProjectRootDirectory);
        ScrollPreviewToCaret();
    }

    /// <summary>The current project's root directory — passed through to PreviewWindow.
    /// UpdateContent, which writes a real preview .html file under it (see HtmlPageShell.
    /// WritePreviewFile) so relative "../images/foo.jpg" references in the body resolve exactly
    /// like they do for the real stored file, one directory below this root.</summary>
    private string? CurrentProjectRootDirectory =>
        ViewModel?.CurrentProject.DirectoryPath;

    /// <summary>
    /// The synthesized "&lt;h1&gt;Chapter N: Title&lt;/h1&gt;" for whatever's currently
    /// selected in the sidebar (see ChapterHeadingHtml) — null for front/back matter (their
    /// heading is already baked into the generated body), when nothing's selected, or when the
    /// given body already opens with its own &lt;h1&gt; (some chapters are authored with their
    /// own heading typed directly into the body — synthesizing another one on top would
    /// duplicate it). Rendering this is what makes Preview/Rich Text mode show the same heading
    /// every export produces for a chapter that doesn't already have one of its own.
    /// </summary>
    private string? ChapterHeadingHtmlFor(string body) =>
        ViewModel?.SelectedSpineItem is { } item ? ChapterHeadingHtml.Build(item, body) : null;

    /// <summary>
    /// Saves the chapter title/subtitle fields, then refreshes whichever of Preview/Rich Text
    /// is currently showing — SaveChapterHeaderCommand alone wouldn't do this, since it mutates
    /// SelectedSpineItem.Title/Subtitle directly rather than through CurrentText, so nothing
    /// would otherwise trigger SyncEditorTextFromViewModel to notice the heading changed.
    /// </summary>
    private void OnSaveChapterHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.SaveChapterHeaderCommand.Execute(null);

        if (IsWysiwygMode)
            PushContentToWysiwyg(CurrentBodyOnly());

        UpdatePreviewWindow(CurrentBodyOnly(), ViewModel.Editor.FilePath is { } path ? Path.GetFileNameWithoutExtension(path) : null);
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e) => ScrollPreviewToCaret();

    private void ScrollPreviewToCaret()
    {
        // Only meaningful while raw mode is what the user is actually typing in — the raw
        // caret doesn't move while editing in WYSIWYG mode, so this would just re-apply a stale
        // fraction; skipping it there means Preview simply stops caret-tracking during WYSIWYG
        // editing rather than jumping to a wrong position.
        if (_previewWindow is null || IsWysiwygMode)
            return;

        var lineCount = EditorTextBox.Document.LineCount;
        var fraction = lineCount <= 1 ? 0 : (double)(EditorTextBox.TextArea.Caret.Line - 1) / (lineCount - 1);
        _previewWindow.ScrollToFraction(fraction);
    }

    private bool IsWysiwygMode => WysiwygToggle.IsChecked == true;

    /// <summary>
    /// Toggles between the raw AvaloniaEdit pane and a WYSIWYG WebView pane over the same
    /// underlying content — see the plan's "three modes" design (raw HTML, WYSIWYG, and the
    /// separate Preview window). Only one is ever visible; per 1.9.1-era history this app has
    /// been burned by AvaloniaEdit going blank after an IsVisible toggle before (see
    /// Directory.Build.props' 1.10.x notes) — that turned out to be an unrelated missing
    /// ControlTheme registration (fixed for good in 1.10.1, in App.axaml, independent of any
    /// instance's IsVisible state), not a consequence of toggling visibility itself, so
    /// reintroducing a toggle here is safe.
    /// </summary>
    private void OnWysiwygToggleClick(object? sender, RoutedEventArgs e)
    {
        if (IsWysiwygMode)
        {
            EditorTextBox.IsVisible = false;
            WysiwygHost.IsVisible = true;
            PushContentToWysiwyg(CurrentBodyOnly());
        }
        else
        {
            WysiwygHost.IsVisible = false;
            EditorTextBox.IsVisible = true;
            EditorTextBox.Focus();
        }
    }

    private void EnsureWysiwygWebView()
    {
        if (_wysiwygWebView is not null)
            return;

        _wysiwygWebView = new NativeWebView();
        _wysiwygWebView.NavigationCompleted += (_, e) => _wysiwygNavigated = e.IsSuccess;
        // Subscribed as a lambda rather than a named method so the compiler infers the exact
        // event-args type instead of this file needing to name it explicitly.
        _wysiwygWebView.WebMessageReceived += (_, e) => OnWysiwygMessageBody(e.Body);
        WysiwygHost.Content = _wysiwygWebView;
    }

    private void PushContentToWysiwyg(string bodyHtml)
    {
        if (ViewModel is null)
            return;

        EnsureWysiwygWebView();
        _wysiwygNavigated = false;
        var html = HtmlPageShell.Wrap(ViewModel.GetCurrentTemplateCss(), bodyHtml, editable: true, ChapterHeadingHtmlFor(bodyHtml));
        if (CurrentProjectRootDirectory is { Length: > 0 } projectRoot)
            _wysiwygWebView!.Navigate(HtmlPageShell.WritePreviewFile(projectRoot, html));
        else
            _wysiwygWebView!.NavigateToString(html, new Uri("about:blank"));
    }

    /// <summary>
    /// Receives every message the WYSIWYG page's JS bridge posts (see HtmlPageShell's
    /// BridgeScript): a { event: "ready" } message the moment window.ebookEditor itself becomes
    /// callable — the signal InvokeWysiwygScript actually gates on, since NativeWebView's own
    /// NavigationCompleted fires once the document has loaded, which isn't guaranteed to land
    /// after this same script has finished running and isn't a safe proxy for "the toolbar can
    /// call into the page now" — and the debounced { event: "change", html: "..." } message
    /// posted after an edit, folded back into CurrentText, guarded by _suppressWysiwygPush so
    /// SyncEditorTextFromViewModel doesn't immediately re-navigate the WebView back to the
    /// content it just sent us. The WYSIWYG page only ever showed the body (see
    /// PushContentToWysiwyg/CurrentBodyOnly), so the edited html here is just the body too —
    /// ChapterFileService.ReplaceBody folds it back in under CurrentText's existing front
    /// matter block, left untouched, rather than overwriting the whole file with body-only text
    /// and losing the front matter entirely.
    /// </summary>
    private void OnWysiwygMessageBody(string? body)
    {
        if (ViewModel is null || body is null)
            return;

        try
        {
            using var message = JsonDocument.Parse(body);
            var eventName = message.RootElement.GetProperty("event").GetString();

            if (eventName == "ready")
            {
                _wysiwygNavigated = true;
                return;
            }

            if (eventName == "contextmenu")
            {
                WysiwygFigureContext? figure = null;
                if (message.RootElement.TryGetProperty("figure", out var figureProp) && figureProp.ValueKind == JsonValueKind.Object)
                {
                    figure = new WysiwygFigureContext(
                        figureProp.GetProperty("id").GetString() ?? "",
                        figureProp.GetProperty("width").GetInt32(),
                        figureProp.GetProperty("height").GetInt32(),
                        figureProp.GetProperty("alignment").GetString() switch
                        {
                            "left" => ImageAlignment.Left,
                            "right" => ImageAlignment.Right,
                            _ => ImageAlignment.Center,
                        },
                        figureProp.GetProperty("flow").GetBoolean(),
                        figureProp.GetProperty("caption").GetString() ?? "");
                }

                ShowWysiwygContextMenu(figure);
                return;
            }

            if (eventName != "change")
                return;

            var editedBody = message.RootElement.GetProperty("html").GetString() ?? string.Empty;
            _suppressWysiwygPush = true;
            ViewModel.Editor.CurrentText = _chapterFileService.ReplaceBody(ViewModel.Editor.CurrentText, editedBody);
            _suppressWysiwygPush = false;
        }
        catch (JsonException)
        {
            // A malformed bridge message shouldn't take the editor down with it.
        }
    }

    /// <summary>
    /// Inserts a fragment of HTML at the cursor in whichever pane is active — the raw
    /// AvaloniaEdit caret, or the WYSIWYG WebView's current DOM selection via its JS bridge (see
    /// HtmlPageShell). Shared by Insert Table/Insert Image/Insert Footnote so those commands
    /// work the same regardless of mode.
    /// </summary>
    private void InsertAtCursor(string html)
    {
        if (IsWysiwygMode)
        {
            InvokeWysiwygScript($"window.ebookEditor.insertHtml({JsonSerializer.Serialize(html)})");
            return;
        }

        var offset = EditorTextBox.CaretOffset;
        EditorTextBox.Document.Insert(offset, html);
        EditorTextBox.CaretOffset = offset + html.Length;
        EditorTextBox.Focus();
    }

    private async void InvokeWysiwygScript(string script)
    {
        if (_wysiwygWebView is null || !_wysiwygNavigated)
            return;

        try
        {
            await _wysiwygWebView.InvokeScript(script);
        }
        catch
        {
            // Best-effort — a script call that races a fresh navigation can legitimately fail.
        }
    }

    /// <summary>
    /// Like InvokeWysiwygScript, but for a bridge call whose actual return value is needed
    /// inline (getSelectionHtml) rather than fired-and-forgotten — NativeWebView.InvokeScript's
    /// Task&lt;string&gt; is the JSON-encoded JS expression result (mirroring WebView2/WKWebView's
    /// own evaluateJavaScript semantics), so a plain JS string return needs one
    /// JsonSerializer.Deserialize to unwrap.
    /// </summary>
    private async Task<string?> InvokeWysiwygScriptForResultAsync(string script)
    {
        if (_wysiwygWebView is null || !_wysiwygNavigated)
            return null;

        try
        {
            var raw = await _wysiwygWebView.InvokeScript(script);
            return raw is null ? null : JsonSerializer.Deserialize<string>(raw);
        }
        catch
        {
            // Best-effort — a script call that races a fresh navigation can legitimately fail.
            return null;
        }
    }

    /// <summary>
    /// Delete/Cut/Copy/Paste, wired to both the toolbar and Window.KeyBindings, working the same
    /// in raw and WYSIWYG mode. Raw mode delegates straight to AvaloniaEdit's own TextEditor
    /// Cut/Copy/Paste/Delete (already fully functional — this app never overrides them). WYSIWYG
    /// mode has no such native equivalent to lean on (nothing in this app has ever wired up
    /// keyboard/clipboard handling for the embedded WebView), so these go through the same
    /// explicit JS-bridge-plus-real-OS-clipboard route every other WYSIWYG command already uses
    /// (see HtmlPageShell's deleteSelection/getSelectionHtml and Avalonia's own IClipboard via
    /// TopLevel — not a JS-only clipboard, so content copied here is pasteable in other apps too).
    /// </summary>
    private void OnWysiwygOrRawDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (IsWysiwygMode)
            InvokeWysiwygScript("window.ebookEditor.deleteSelection()");
        else
            EditorTextBox.Delete();
    }

    private async void OnWysiwygOrRawCutClick(object? sender, RoutedEventArgs e)
    {
        if (!IsWysiwygMode)
        {
            EditorTextBox.Cut();
            return;
        }

        var html = await InvokeWysiwygScriptForResultAsync("window.ebookEditor.getSelectionHtml()");
        if (string.IsNullOrEmpty(html))
            return;

        await SetClipboardTextAsync(html);
        InvokeWysiwygScript("window.ebookEditor.deleteSelection()");
    }

    private async void OnWysiwygOrRawCopyClick(object? sender, RoutedEventArgs e)
    {
        if (!IsWysiwygMode)
        {
            EditorTextBox.Copy();
            return;
        }

        var html = await InvokeWysiwygScriptForResultAsync("window.ebookEditor.getSelectionHtml()");
        if (!string.IsNullOrEmpty(html))
            await SetClipboardTextAsync(html);
    }

    private async void OnWysiwygOrRawPasteClick(object? sender, RoutedEventArgs e)
    {
        if (!IsWysiwygMode)
        {
            EditorTextBox.Paste();
            return;
        }

        var text = await GetClipboardTextAsync();
        if (!string.IsNullOrEmpty(text))
            InsertAtCursor(text);
    }

    private async Task SetClipboardTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private async Task<string?> GetClipboardTextAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        return clipboard is null ? null : await clipboard.TryGetTextAsync();
    }

    private async void OnInsertTableClick(object? sender, RoutedEventArgs e)
    {
        var window = new InsertTableWindow();
        await window.ShowDialog(this);

        if (window.Result is not { } html)
            return;

        InsertAtCursor(html);
    }

    /// <summary>
    /// Picks an image (starting in the project's images/ folder, created here if it doesn't
    /// exist yet), copies it in if it was picked from elsewhere, then opens InsertImageWindow
    /// (seeded with the file's own natural pixel dimensions, clamped to fit the project's chosen
    /// PDF page size) for width/height/alignment/flow/caption, and inserts it as a real HTML
    /// &lt;figure&gt; — its own explicit size and InternalLinkConvention.ToFigureStyle
    /// alignment/flow, grouping the image with a ".caption"-styled &lt;figcaption&gt; underneath
    /// — see EditorStyleCatalog. The figure gets its own InternalLinkConvention.FigureIdPrefix
    /// id so the List of Figures/Photos page can link back to it.
    /// </summary>
    private async void OnInsertImageClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(StorageProvider, ViewModel.CurrentProject.ImagesDir, "Insert Image");
        if (fileName is null)
            return;

        var imagePath = Path.Combine(ViewModel.CurrentProject.ImagesDir, fileName);
        var (naturalWidth, naturalHeight) = TryReadPixelSize(imagePath) ?? (400, 300);
        var pageSize = PdfPageSizeCatalog.Resolve(ViewModel.CurrentProject.Metadata.PdfPageSize);
        var defaultCaption = Path.GetFileNameWithoutExtension(fileName);

        var dialog = new InsertImageWindow(naturalWidth, naturalHeight, pageSize, defaultCaption);
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } result)
            return;

        var placement = new ImagePlacement(result.Alignment, result.Flow);
        var altText = System.Net.WebUtility.HtmlEncode(defaultCaption);
        var caption = System.Net.WebUtility.HtmlEncode(result.Caption);
        var figureId = $"{InternalLinkConvention.FigureIdPrefix}{Guid.NewGuid():N}";
        var html = $"""
            <figure id="{figureId}" style="{placement.ToFigureStyle()}">
            <img src="../images/{fileName}" alt="{altText}" width="{result.Width}" height="{result.Height}">
            <figcaption class="caption">{caption}</figcaption>
            </figure>
            """;

        InsertAtCursor(html);
    }

    private static (int Width, int Height)? TryReadPixelSize(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            return (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch
        {
            // Best-effort — falls back to InsertImageWindow's own default size if the file
            // can't be decoded (an unsupported/corrupt format).
            return null;
        }
    }

    /// <summary>A right-clicked &lt;figure&gt;'s current state, as read back out of the DOM by
    /// the JS bridge's "contextmenu" listener (see HtmlPageShell) — enough to reopen
    /// InsertImageWindow pre-filled and, on confirm, write the changes back via updateFigure.</summary>
    private sealed record WysiwygFigureContext(string Id, int Width, int Height, ImageAlignment Alignment, bool Flow, string Caption);

    /// <summary>
    /// WYSIWYG mode's right-click menu — built and opened here rather than declared as a static
    /// Avalonia ContextMenu (compare EditorTextBox's own, in XAML), since a right-click inside
    /// the embedded native WebView's own content is handled by the WebView itself, not routed
    /// through Avalonia's control tree the way it is for ordinary controls; the JS bridge's
    /// "contextmenu" listener suppresses the native menu and posts the click here instead (see
    /// HtmlPageShell). Mirrors the raw editor's own context menu items plus the Phase B
    /// Cut/Copy/Paste/Delete commands, plus "Edit Image…" — only enabled when the right-click
    /// landed on a &lt;figure&gt;.
    /// </summary>
    private void ShowWysiwygContextMenu(WysiwygFigureContext? figure)
    {
        var menu = new ContextMenu();

        void AddItem(string header, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        AddItem("Cut", () => OnWysiwygOrRawCutClick(this, new RoutedEventArgs()));
        AddItem("Copy", () => OnWysiwygOrRawCopyClick(this, new RoutedEventArgs()));
        AddItem("Paste", () => OnWysiwygOrRawPasteClick(this, new RoutedEventArgs()));
        AddItem("Delete", () => OnWysiwygOrRawDeleteClick(this, new RoutedEventArgs()));
        menu.Items.Add(new Separator());
        AddItem("Bold", () => WrapSelectionInTag("strong"));
        AddItem("Italic", () => WrapSelectionInTag("em"));
        AddItem("Insert Element", () => OnInsertElementButtonClick(this, new RoutedEventArgs()));
        AddItem("Insert Table…", () => OnInsertTableClick(this, new RoutedEventArgs()));
        AddItem("Insert Image…", () => OnInsertImageClick(this, new RoutedEventArgs()));
        AddItem("Insert Footnote…", () => OnInsertFootnoteClick(this, new RoutedEventArgs()));
        AddItem("Mark Link Destination…", () => OnMarkLinkDestinationClick(this, new RoutedEventArgs()));
        AddItem("Insert Internal Link…", () => OnInsertInternalLinkClick(this, new RoutedEventArgs()));
        AddItem("Mark as Index Entry…", () => OnMarkIndexEntryClick(this, new RoutedEventArgs()));
        AddItem("Apply Style", () => OnApplyStyleButtonClick(this, new RoutedEventArgs()));
        menu.Items.Add(new Separator());
        AddItem("Edit Image…", () => OnEditFigureClick(figure!), enabled: figure is not null);

        menu.Open(WysiwygHost);
    }

    /// <summary>
    /// Right-click "Edit Image…": reopens InsertImageWindow pre-filled with the figure's current
    /// width/height/alignment/flow/caption (rather than a freshly-picked file's natural size),
    /// and on confirm updates that same &lt;figure&gt; in place via the updateFigure bridge call
    /// instead of inserting a new one.
    /// </summary>
    private async void OnEditFigureClick(WysiwygFigureContext figure)
    {
        if (ViewModel is null)
            return;

        var pageSize = PdfPageSizeCatalog.Resolve(ViewModel.CurrentProject.Metadata.PdfPageSize);
        var dialog = new InsertImageWindow(
            figure.Width, figure.Height, pageSize, figure.Caption,
            figure.Alignment, figure.Flow, title: "Edit Image");
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } result)
            return;

        var placement = new ImagePlacement(result.Alignment, result.Flow);
        var caption = System.Net.WebUtility.HtmlEncode(result.Caption);
        InvokeWysiwygScript(
            $"window.ebookEditor.updateFigure({JsonSerializer.Serialize(figure.Id)}, {result.Width}, {result.Height}, {JsonSerializer.Serialize(placement.ToFigureStyle())}, {JsonSerializer.Serialize(caption)})");
    }

    private void OnEditorContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        var hasSelection = EditorTextBox.SelectionLength > 0;
        ApplyStyleMenuItem.IsEnabled = hasSelection;
        InsertElementMenuItem.IsEnabled = hasSelection;
        if (!hasSelection)
            return;

        ApplyStyleMenuItem.Items.Clear();
        foreach (var style in EditorStyleCatalog.Styles)
        {
            var item = new MenuItem { Header = style.Label, Tag = style };
            item.Click += OnApplyStyleClick;
            ApplyStyleMenuItem.Items.Add(item);
        }

        InsertElementMenuItem.Items.Clear();
        foreach (var element in HtmlElementCatalog.Elements)
        {
            var item = new MenuItem { Header = element.Label, Tag = element };
            item.Click += OnInsertElementClick;
            InsertElementMenuItem.Items.Add(item);
        }
    }

    private void OnBoldClick(object? sender, RoutedEventArgs e) => WrapSelectionInTag("strong");

    private void OnItalicClick(object? sender, RoutedEventArgs e) => WrapSelectionInTag("em");

    /// <summary>
    /// The toolbar's "Insert Element ▾" button — the WYSIWYG-mode-reachable equivalent of the
    /// raw editor's right-click "Insert Element" submenu, mirroring OnApplyStyleButtonClick's
    /// shape. Lists and Horizontal Rule aren't selection-wrap operations like the catalog
    /// entries (headings/paragraph/blockquote), so they're inserted directly via InsertAtCursor
    /// instead, appended to the same flyout.
    /// </summary>
    private void OnInsertElementButtonClick(object? sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        foreach (var element in HtmlElementCatalog.Elements)
        {
            var item = new MenuItem { Header = element.Label, Tag = element };
            item.Click += OnInsertElementClick;
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new Separator());

        var bulletedList = new MenuItem { Header = "Bulleted List" };
        bulletedList.Click += (_, _) => InsertList(ordered: false);
        flyout.Items.Add(bulletedList);

        var numberedList = new MenuItem { Header = "Numbered List" };
        numberedList.Click += (_, _) => InsertList(ordered: true);
        flyout.Items.Add(numberedList);

        var horizontalRule = new MenuItem { Header = "Horizontal Rule" };
        horizontalRule.Click += (_, _) => InsertAtCursor("<hr>\n");
        flyout.Items.Add(horizontalRule);

        flyout.ShowAt(InsertElementButton);
    }

    private void OnInsertElementClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: HtmlElementOption element })
            return;

        WrapSelectionInTag(element.Tag);
    }

    /// <summary>
    /// Inserts a new 3-item list at the cursor/selection — each &lt;li&gt; a placeholder the
    /// user replaces, since (unlike a heading/blockquote) a selection rarely maps cleanly onto
    /// "one list item per line" without more UI than a toolbar button justifies.
    /// </summary>
    private void InsertList(bool ordered)
    {
        var tag = ordered ? "ol" : "ul";
        InsertAtCursor($"<{tag}>\n<li>List item</li>\n<li>List item</li>\n<li>List item</li>\n</{tag}>\n");
    }

    /// <summary>
    /// Wraps the current selection in a plain "&lt;tag&gt;...&lt;/tag&gt;" with no class
    /// attribute — the mechanic behind Bold/Italic and Insert Element's headings/paragraph/
    /// blockquote entries. Same shape as OnApplyStyleClick, minus the CSS class: requires a
    /// real selection in raw mode (silently no-ops without one, matching Apply Style's own
    /// raw-mode behavior); WYSIWYG mode's wrapSelection is likewise a no-op with nothing
    /// selected in the WebView's DOM.
    /// </summary>
    private void WrapSelectionInTag(string tag)
    {
        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.wrapSelection({JsonSerializer.Serialize(tag)}, null)");
            return;
        }

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength <= 0)
            return;

        var selectedText = EditorTextBox.Document.GetText(selectionStart, selectionLength);
        var wrapped = $"<{tag}>{selectedText}</{tag}>";

        EditorTextBox.Document.Replace(selectionStart, selectionLength, wrapped);
        EditorTextBox.CaretOffset = selectionStart + wrapped.Length;
        EditorTextBox.Focus();
    }

    /// <summary>
    /// The toolbar's "Apply Style ▾" button — the WYSIWYG-mode-reachable equivalent of the raw
    /// editor's right-click "Apply Style" submenu (that context menu is attached to EditorTextBox
    /// itself, so it's only reachable while the raw pane is the one visible/focused). Builds the
    /// same menu items OnEditorContextMenuOpened does, reusing OnApplyStyleClick as the handler.
    /// </summary>
    private void OnApplyStyleButtonClick(object? sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        foreach (var style in EditorStyleCatalog.Styles)
        {
            var item = new MenuItem { Header = style.Label, Tag = style };
            item.Click += OnApplyStyleClick;
            flyout.Items.Add(item);
        }

        flyout.ShowAt(ApplyStyleButton);
    }

    /// <summary>
    /// Wraps the current selection in real HTML naming the CSS class the chosen style hooks to
    /// in DefaultStylesheet.cs/"Vellum Serif.css" — a &lt;span&gt; for an inline style (safe
    /// mid-paragraph, e.g. small-caps a single word) or a &lt;div&gt; for a block style (its
    /// own paragraph-level element, e.g. a verse stanza) — see EditorStyleCatalog.IsBlock. In
    /// WYSIWYG mode the wrap happens JS-side against the WebView's own DOM selection (see
    /// HtmlPageShell's wrapSelection) rather than against EditorTextBox's selection, which isn't
    /// what the user is looking at in that mode.
    /// </summary>
    private void OnApplyStyleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EditorStyle style })
            return;

        var tag = style.IsBlock ? "div" : "span";

        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.wrapSelection({JsonSerializer.Serialize(tag)}, {JsonSerializer.Serialize(style.ClassName)})");
            return;
        }

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength <= 0)
            return;

        var selectedText = EditorTextBox.Document.GetText(selectionStart, selectionLength);
        var wrapped = $"<{tag} class=\"{style.ClassName}\">{selectedText}</{tag}>";

        EditorTextBox.Document.Replace(selectionStart, selectionLength, wrapped);
        EditorTextBox.CaretOffset = selectionStart + wrapped.Length;
        EditorTextBox.Focus();
    }

    /// <summary>
    /// Inserts a numbered footnote reference at the cursor — "&lt;sup id="fnref:N"&gt;&lt;a
    /// href="#fn:N" class="footnote-ref"&gt;N&lt;/a&gt;&lt;/sup&gt;" — and adds the note text to
    /// (or starts) a "&lt;div class="footnotes"&gt;" list at the end of the page. This is the
    /// same HTML shape Markdig's footnote extension used to produce for the EPUB (see
    /// DefaultStylesheet.cs's .footnote-ref/.footnotes/.footnote-back-ref rules, which already
    /// style it), chosen so those existing rules keep working without changes. The number is
    /// the highest "fnref:" id already in the page's text, plus one — footnote numbering is
    /// per-page, matching how footnotes always worked here (each chapter's notes are its own,
    /// not book-wide).
    /// </summary>
    private async void OnInsertFootnoteClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var dialog = new InsertFootnoteWindow();
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } noteText)
            return;

        // Scans ViewModel.Editor.CurrentText, not EditorTextBox.Text directly — the latter only
        // reflects live typing in raw mode; WYSIWYG-mode edits reach CurrentText via the
        // debounced JS bridge instead (see OnWysiwygMessageBody), so it's the one source both
        // modes keep current.
        var nextNumber = FootnoteReferenceIdRegex().Matches(ViewModel.Editor.CurrentText)
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max() + 1;

        var reference = $"<sup id=\"fnref:{nextNumber}\"><a href=\"#fn:{nextNumber}\" class=\"footnote-ref\">{nextNumber}</a></sup>";
        InsertAtCursor(reference);
        InsertOrAppendFootnoteDefinition(nextNumber, noteText);
    }

    /// <summary>
    /// Adds a footnote's note text to (or starts) a "&lt;div class="footnotes"&gt;" list at the
    /// end of the page. Raw mode finds/edits that block as a string, by offset, within
    /// EditorTextBox's own document; WYSIWYG mode instead does the equivalent DOM operation
    /// JS-side (see HtmlPageShell's appendFootnoteDefinition) since there's no reliable mapping
    /// from a string offset in CurrentText to a DOM position in the WebView's live document.
    /// </summary>
    private void InsertOrAppendFootnoteDefinition(int number, string noteText)
    {
        var encodedNote = System.Net.WebUtility.HtmlEncode(noteText);

        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.appendFootnoteDefinition({number}, {JsonSerializer.Serialize(encodedNote)})");
            return;
        }

        var listItem = $"<li id=\"fn:{number}\"><p>{encodedNote} <a href=\"#fnref:{number}\" class=\"footnote-back-ref\">↩</a></p></li>";

        var text = EditorTextBox.Text ?? "";
        var footnotesIndex = text.IndexOf("<div class=\"footnotes\">", StringComparison.Ordinal);
        if (footnotesIndex >= 0)
        {
            var closeIndex = text.IndexOf("</ol>", footnotesIndex, StringComparison.Ordinal);
            if (closeIndex >= 0)
            {
                EditorTextBox.Document.Insert(closeIndex, listItem + "\n");
                return;
            }
        }

        var newBlock = $"\n\n<div class=\"footnotes\">\n<hr>\n<ol>\n{listItem}\n</ol>\n</div>\n";
        EditorTextBox.Document.Insert(EditorTextBox.Document.TextLength, newBlock);
    }

    [System.Text.RegularExpressions.GeneratedRegex("id=\"fnref:(\\d+)\"")]
    private static partial System.Text.RegularExpressions.Regex FootnoteReferenceIdRegex();

    /// <summary>
    /// Wraps the current selection in "&lt;span id="dest:{slug}"&gt;" — a cross-document link
    /// target another chapter's "Insert Internal Link" can jump to (see
    /// InternalLinkConvention). The dialog's label only feeds the slug; the actual wrapped
    /// content is whatever's currently selected, exactly like OnApplyStyleClick's own selection-
    /// wrap. Raw mode requires a real selection (silently no-ops without one, matching
    /// OnApplyStyleClick's own raw-mode behavior); WYSIWYG mode's JS-side wrapSelectionWithId is
    /// itself a no-op with nothing selected in the WebView's DOM.
    /// </summary>
    private async void OnMarkLinkDestinationClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var selectedText = !IsWysiwygMode && EditorTextBox.SelectionLength > 0
            ? EditorTextBox.Document.GetText(EditorTextBox.SelectionStart, EditorTextBox.SelectionLength)
            : null;

        var dialog = new MarkLinkDestinationWindow(selectedText);
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } label)
            return;

        var destinationId = $"{InternalLinkConvention.DestinationIdPrefix}{UniqueDestinationSlug(label)}";

        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.wrapSelectionWithId({JsonSerializer.Serialize(destinationId)})");
            return;
        }

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength <= 0)
            return;

        var selectedRaw = EditorTextBox.Document.GetText(selectionStart, selectionLength);
        var wrapped = $"<span id=\"{destinationId}\">{selectedRaw}</span>";
        EditorTextBox.Document.Replace(selectionStart, selectionLength, wrapped);
        EditorTextBox.CaretOffset = selectionStart + wrapped.Length;
        EditorTextBox.Focus();
    }

    /// <summary>
    /// A destination's HTML id must be unique within its own page — appends "-2", "-3", etc. to
    /// the label-derived slug until no "dest:" id with that value is already present in the
    /// page's own text. Checks ViewModel.Editor.CurrentText (kept current in both modes — see
    /// OnWysiwygMessageBody's debounced sync from the WYSIWYG bridge) rather than the DOM
    /// directly, so this works identically regardless of which pane is active.
    /// </summary>
    private string UniqueDestinationSlug(string label) => UniqueMarkerSlug(label, InternalLinkConvention.DestinationIdPrefix, "destination");

    /// <summary>Shared by both "Mark Link Destination" and "Mark as Index Entry"'s single-
    /// occurrence path — a marker's HTML id must be unique within its own page; appends "-2",
    /// "-3", etc. to the label-derived slug until no id with that value (under the given
    /// prefix) is already present in the page's own text.</summary>
    private string UniqueMarkerSlug(string label, string idPrefix, string fallback)
    {
        var baseSlug = Slug.Create(label, fallback);
        var slug = baseSlug;
        var suffix = 2;
        while (ViewModel!.Editor.CurrentText.Contains($"id=\"{idPrefix}{slug}\"", StringComparison.Ordinal))
            slug = $"{baseSlug}-{suffix++}";

        return slug;
    }

    /// <summary>
    /// Scans every chapter/page in the project for "Mark Link Destination" markers (see
    /// DocumentLinkDestinationScanner); if none exist yet, tells the user to mark one first
    /// rather than opening an empty picker. Otherwise wraps the current selection in a real
    /// link to the chosen destination, or — since there's nothing to wrap when nothing is
    /// selected — inserts a brand new link using the destination's own marked text.
    /// </summary>
    private async void OnInsertInternalLinkClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var destinations = _linkDestinationScanner.FindAll(ViewModel.CurrentProject);
        if (destinations.Count == 0)
        {
            await new MessageWindow("Insert Internal Link",
                "A Link Destination must be created before you can link to it.").ShowDialog(this);
            return;
        }

        var dialog = new InsertInternalLinkWindow(destinations);
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } destination)
            return;

        var href = $"{destination.Item.RelativePath}#{destination.DestinationId}";

        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.insertOrWrapLink({JsonSerializer.Serialize(href)}, {JsonSerializer.Serialize(destination.Label)})");
            return;
        }

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength > 0)
        {
            var selectedRaw = EditorTextBox.Document.GetText(selectionStart, selectionLength);
            var wrapped = $"<a href=\"{href}\">{selectedRaw}</a>";
            EditorTextBox.Document.Replace(selectionStart, selectionLength, wrapped);
            EditorTextBox.CaretOffset = selectionStart + wrapped.Length;
            EditorTextBox.Focus();
        }
        else
        {
            InsertAtCursor($"<a href=\"{href}\">{System.Net.WebUtility.HtmlEncode(destination.Label)}</a>");
        }
    }

    /// <summary>
    /// "Mark as Index Entry…" — either wraps just the current selection in a single new
    /// index-entry marker (see InternalLinkConvention), or, if the dialog's "mark all
    /// occurrences" checkbox was checked, marks every case-insensitive occurrence of the term
    /// found anywhere in the current chapter (WYSIWYG mode: HtmlPageShell's own
    /// markAllOccurrences JS function walks the live DOM; raw mode: IndexEntryMarker walks the
    /// parsed body and IndexEntryScanner-equivalent HTML string, then the whole body is
    /// reassigned via ViewModel.Editor.CurrentText — MainWindow's own CurrentText-changed
    /// subscription pushes that into EditorTextBox automatically, so no direct
    /// EditorTextBox.Text assignment is needed here).
    /// </summary>
    private async void OnMarkIndexEntryClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var selectedText = !IsWysiwygMode && EditorTextBox.SelectionLength > 0
            ? EditorTextBox.Document.GetText(EditorTextBox.SelectionStart, EditorTextBox.SelectionLength)
            : null;

        var dialog = new MarkIndexEntryWindow(selectedText);
        await dialog.ShowDialog(this);

        if (dialog.Result is not { } result)
            return;

        if (result.MarkAllOccurrences)
        {
            if (IsWysiwygMode)
            {
                InvokeWysiwygScript($"window.ebookEditor.markAllOccurrences({JsonSerializer.Serialize(result.Term)})");
                return;
            }

            var currentText = ViewModel.Editor.CurrentText;
            var (_, body) = _chapterFileService.ParseChapter(currentText);
            var markedBody = IndexEntryMarker.MarkAllOccurrences(body, result.Term);
            if (markedBody != body)
                ViewModel.Editor.CurrentText = _chapterFileService.ReplaceBody(currentText, markedBody);
            return;
        }

        var indexId = $"{InternalLinkConvention.IndexEntryIdPrefix}{UniqueMarkerSlug(result.Term, InternalLinkConvention.IndexEntryIdPrefix, "term")}";

        if (IsWysiwygMode)
        {
            InvokeWysiwygScript(
                $"window.ebookEditor.wrapSelectionAsIndexEntry({JsonSerializer.Serialize(result.Term)}, {JsonSerializer.Serialize(indexId)})");
            return;
        }

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength <= 0)
            return;

        var selectedRaw = EditorTextBox.Document.GetText(selectionStart, selectionLength);
        var wrapped = $"<span class=\"index-entry\" data-index-term=\"{System.Net.WebUtility.HtmlEncode(result.Term)}\" id=\"{indexId}\">{selectedRaw}</span>";
        EditorTextBox.Document.Replace(selectionStart, selectionLength, wrapped);
        EditorTextBox.CaretOffset = selectionStart + wrapped.Length;
        EditorTextBox.Focus();
    }

    private void OnEditorTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged || ViewModel is null)
            return;

        ViewModel.Editor.CurrentText = EditorTextBox.Text ?? string.Empty;
    }

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        var wizard = new NewProjectWizardWindow();
        await wizard.ShowDialog(this);

        if (wizard.CreatedProject is { } project)
            OpenProjectInNewWindow(project);
    }

    private async void OnCreateProjectFromEpubClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an ePub File",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("EPUB") { Patterns = ["*.epub"] }]
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } epubPath)
            return;

        var wizard = new EpubImportWizardWindow(epubPath);
        await wizard.ShowDialog(this);

        if (wizard.CreatedProject is { } project)
            OpenProjectInNewWindow(project);
    }

    /// <summary>
    /// Both failure paths here previously only set ViewModel.StatusMessage — a small,
    /// low-opacity status-bar TextBlock a user can easily miss entirely, especially since
    /// nothing else visibly happens (no new window opens) — so a real failure read as the
    /// command having silently done nothing. Both now show a real, impossible-to-miss dialog
    /// instead. TryGetLocalPath returning null for a folder the user actually picked (as
    /// opposed to the picker returning nothing at all, i.e. the user cancelled, which stays
    /// silent) is a real, if rare, failure mode of its own — an untrusted/security-scoped
    /// location the picker couldn't resolve to a plain filesystem path.
    /// </summary>
    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open eBook Editor Project",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
            return;

        var path = folder.TryGetLocalPath();
        if (path is null)
        {
            await new MessageWindow("Open Project",
                "That folder couldn't be opened — its location isn't accessible as a regular file path.").ShowDialog(this);
            return;
        }

        try
        {
            var result = new ProjectService().LoadProject(path);
            OpenProjectInNewWindow(result.Project);
            if (result.MissingSpineItemPaths.Count > 0)
                await new MessageWindow("Open Project", BuildMissingSpineItemsMessage(result.MissingSpineItemPaths)).ShowDialog(this);
        }
        catch (Exception ex)
        {
            await new MessageWindow("Open Project", $"Couldn't open a project at:\n{path}\n\n{ex.Message}").ShowDialog(this);
        }
    }

    /// <summary>Shared by OnOpenProjectClick/OpenRecentProject — a project referencing a
    /// content file that's missing on disk (moved/deleted outside the app) now loads anyway
    /// with that item excluded, rather than failing outright; this is the notice shown for
    /// it.</summary>
    private static string BuildMissingSpineItemsMessage(IReadOnlyList<string> missingPaths) =>
        $"The project opened, but {(missingPaths.Count == 1 ? "this file was" : $"these {missingPaths.Count} files were")} missing on disk and {(missingPaths.Count == 1 ? "has" : "have")} been excluded:\n\n{string.Join("\n", missingPaths)}";

    private static void OpenProjectInNewWindow(EbookProject project)
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel(project) };
        window.Show();
    }

    private void OnRecentProjectsSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        RecentProjectsMenuItem.Items.Clear();

        var recentPaths = ViewModel?.GetRecentProjectPaths() ?? [];
        if (recentPaths.Count == 0)
        {
            RecentProjectsMenuItem.Items.Add(new MenuItem { Header = "(No recent projects)", IsEnabled = false });
            return;
        }

        foreach (var path in recentPaths)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => OpenRecentProject(path);
            RecentProjectsMenuItem.Items.Add(item);
        }
    }

    private async void OpenRecentProject(string path)
    {
        try
        {
            var result = new ProjectService().LoadProject(path);
            OpenProjectInNewWindow(result.Project);
            if (result.MissingSpineItemPaths.Count > 0)
                await new MessageWindow("Open Project", BuildMissingSpineItemsMessage(result.MissingSpineItemPaths)).ShowDialog(this);
        }
        catch (Exception ex)
        {
            if (ViewModel is not null)
                ViewModel.StatusMessage = $"Couldn't open recent project at {path}: {ex.Message}";
        }
    }

    private async void OnEditFrontMatterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await new FrontMatterWindow(ViewModel).ShowDialog(this);
    }

    private async void OnEditStyleClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.RefreshAvailableTemplates();
        await new StyleWindow(ViewModel).ShowDialog(this);
    }

    private async void OnEditCopyrightPublishingClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await new CopyrightPublishingWindow(ViewModel).ShowDialog(this);
    }

    private async void OnEditGenreTagsClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await new GenreTagsWindow(ViewModel).ShowDialog(this);
    }

    private async void OnEditAboutTheAuthorClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await new AboutTheAuthorWindow(ViewModel).ShowDialog(this);
    }

    private async void OnEditPdfSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        await new PdfSettingsWindow(ViewModel).ShowDialog(this);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow();
        await dialog.ShowDialog(this);
    }

    private async void OnImportDocxClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Word Document",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Word Document") { Patterns = ["*.docx"] }]
        });

        var file = files.FirstOrDefault();
        if (file?.TryGetLocalPath() is { } localPath)
            ViewModel.ImportDocx(localPath);
    }

    private async void OnImportChaptersClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Chapters",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Chapter files") { Patterns = ["*.ebhtml", "*.md", "*.docx", "*.html", "*.htm"] }
            ]
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (paths.Count > 0)
            ViewModel.ImportChapterFiles(paths);
    }

    // Same rationale as the internal spine-reorder drag above for using the classic
    // IDataObject API rather than the newer typed DataTransfer one.
#pragma warning disable CS0618
    private void OnSidebarDragOver(object? sender, DragEventArgs e)
    {
        if (GetDroppedFilePaths(e.Data).Count > 0)
            e.DragEffects = DragDropEffects.Copy;
    }

    private void OnSidebarDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is null)
            return;

        var filePaths = GetDroppedFilePaths(e.Data);
        if (filePaths.Count > 0)
            ViewModel.ImportChapterFiles(filePaths);
    }

    private static IReadOnlyList<string> GetDroppedFilePaths(IDataObject data)
    {
        if (data.Get(DataFormats.Files) is IEnumerable<IStorageItem> items)
            return items.Select(i => i.TryGetLocalPath()).Where(p => p is not null).Select(p => p!).ToList();

        if (data.Get(DataFormats.FileNames) is IEnumerable<string> fileNames)
            return fileNames.ToList();

        return [];
    }
#pragma warning restore CS0618

    private void OnSpineItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || SpineListBox.SelectedItem is not SpineItem item)
            return;

        ViewModel.OpenSpineItem(item);
    }

    private async void OnDeleteChapterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { DataContext: SpineItem { Type: SpineItemType.Chapter } item })
            return;

        var dialog = new ConfirmDialog("Delete Chapter", $"Delete \"{item.Title}\"? This can't be undone.", "Delete");
        if (await dialog.ShowDialog<bool>(this))
            ViewModel.DeleteChapter(item);
    }

    /// <summary>The sidebar's "+ Add ▾" button — mirrors OnApplyStyleButtonClick's
    /// programmatically-built MenuFlyout pattern, since each option maps to its own
    /// no-argument RelayCommand on the view model.</summary>
    private void OnAddItemButtonClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var flyout = new MenuFlyout();

        var chapter = new MenuItem { Header = "Chapter" };
        chapter.Click += (_, _) => ViewModel.AddChapterCommand.Execute(null);
        flyout.Items.Add(chapter);

        var partBreak = new MenuItem { Header = "Part Break" };
        partBreak.Click += (_, _) => ViewModel.AddPartBreakCommand.Execute(null);
        flyout.Items.Add(partBreak);

        var frontMatterPage = new MenuItem { Header = "Front Matter Page" };
        frontMatterPage.Click += (_, _) => ViewModel.AddFrontMatterPageCommand.Execute(null);
        flyout.Items.Add(frontMatterPage);

        var backMatterPage = new MenuItem { Header = "Back Matter Page" };
        backMatterPage.Click += (_, _) => ViewModel.AddBackMatterPageCommand.Execute(null);
        flyout.Items.Add(backMatterPage);

        flyout.ShowAt(AddItemButton);
    }

    /// <summary>Shows "Move Up"/"Move Down" only for custom front/back-matter pages (chapters
    /// reposition via drag-and-drop instead — see OnSpineItemPointerMoved), and disables
    /// whichever direction would move past its group's edge.</summary>
    private void OnSpineItemContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not ContextMenu { PlacementTarget: Control { DataContext: SpineItem item } } contextMenu)
            return;

        var canMove = item.Type is SpineItemType.FrontMatter or SpineItemType.BackMatter;
        var ordered = ViewModel.CurrentProject.Spine.OrderBy(i => i.Order).ToList();
        var index = ordered.FindIndex(i => i.Id == item.Id);
        var canMoveUp = canMove && index > 0 && ordered[index - 1].Type == item.Type;
        var canMoveDown = canMove && index < ordered.Count - 1 && ordered[index + 1].Type == item.Type;

        foreach (var menuItem in contextMenu.Items.OfType<MenuItem>())
        {
            switch (menuItem.Header as string)
            {
                case "Move Up":
                    menuItem.IsVisible = canMove;
                    menuItem.IsEnabled = canMoveUp;
                    break;
                case "Move Down":
                    menuItem.IsVisible = canMove;
                    menuItem.IsEnabled = canMoveDown;
                    break;
            }
        }
    }

    private void OnMoveItemUpClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { DataContext: SpineItem item })
            return;

        ViewModel.MoveItem(item, SpineMoveDirection.Up);
    }

    private void OnMoveItemDownClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { DataContext: SpineItem item })
            return;

        ViewModel.MoveItem(item, SpineMoveDirection.Down);
    }

    private async void OnUpgradeProjectToHtmlClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (!ViewModel.ProjectNeedsHtmlMigration)
        {
            ViewModel.StatusMessage = "This project is already using the current HTML format.";
            return;
        }

        var dialog = new ConfirmDialog(
            "Upgrade Project to HTML",
            "This converts every chapter and page in this project from Markdown to this app's HTML format. A full backup copy of the project is made first, then the original files are replaced. Continue?",
            "Upgrade");
        if (await dialog.ShowDialog<bool>(this))
            ViewModel.UpgradeProjectToHtml();
    }

    private void OnExportChapterAsWordClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { DataContext: SpineItem { Type: SpineItemType.Chapter } item })
            return;

        ViewModel.ExportChapterAsWord(item);
    }

    private void OnSpineItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: SpineItem { Type: SpineItemType.Chapter } item })
        {
            _dragCandidate = item;
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private async void OnSpineItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var current = e.GetPosition(this);
        var dx = current.X - _dragStartPoint.X;
        var dy = current.Y - _dragStartPoint.Y;
        if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
            return;

        var item = _dragCandidate;
        _dragCandidate = null;

        // Avalonia 11.3's replacement DataTransfer/DoDragDropAsync API requires typed
        // DataFormat<T> descriptors with no simple string-keyed Set/Get; the older
        // DataObject-based API below remains fully functional for this in-process,
        // same-app drag payload, so it's used deliberately rather than chasing the
        // newer (and here, more elaborate) surface.
#pragma warning disable CS0618
        var data = new DataObject();
        data.Set(SpineItemIdFormat, item.Id.ToString());
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
#pragma warning restore CS0618
    }

    private void OnSpineItemDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        e.DragEffects = e.Data.Contains(SpineItemIdFormat) ? DragDropEffects.Move : DragDropEffects.None;
#pragma warning restore CS0618
    }

    private void OnSpineItemDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is null || sender is not Control { DataContext: SpineItem { Type: SpineItemType.Chapter } targetItem })
            return;

#pragma warning disable CS0618
        var draggedIdText = e.Data.Get(SpineItemIdFormat) as string;
#pragma warning restore CS0618
        if (draggedIdText is null || !Guid.TryParse(draggedIdText, out var draggedId))
            return;

        if (draggedId == targetItem.Id)
            return;

        var chapterIds = ViewModel.CurrentProject.Spine
            .Where(i => i.Type == SpineItemType.Chapter)
            .OrderBy(i => i.Order)
            .Select(i => i.Id)
            .ToList();

        chapterIds.Remove(draggedId);
        var targetIndex = chapterIds.IndexOf(targetItem.Id);
        chapterIds.Insert(targetIndex, draggedId);

        ViewModel.ReorderChapters(chapterIds);
    }
}

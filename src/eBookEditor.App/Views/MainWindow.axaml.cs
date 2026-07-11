using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        _previewWindow.UpdateContent(ViewModel.GetCurrentTemplateCss(), body, title, ChapterHeadingHtmlFor(body));
        ScrollPreviewToCaret();
    }

    private void UpdatePreviewWindow(string bodyHtml, string? title)
    {
        if (_previewWindow is null || ViewModel is null)
            return;

        _previewWindow.UpdateContent(ViewModel.GetCurrentTemplateCss(), bodyHtml, title, ChapterHeadingHtmlFor(bodyHtml));
        ScrollPreviewToCaret();
    }

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
        _wysiwygWebView!.NavigateToString(html, new Uri("about:blank"));
    }

    /// <summary>
    /// Receives the debounced { event: "change", html: "..." } message the WYSIWYG page's JS
    /// bridge posts after an edit (see HtmlPageShell's BridgeScript) and folds it back into
    /// CurrentText — guarded by _suppressWysiwygPush so SyncEditorTextFromViewModel doesn't
    /// immediately re-navigate the WebView back to the content it just sent us. The WYSIWYG
    /// page only ever showed the body (see PushContentToWysiwyg/CurrentBodyOnly), so the edited
    /// html here is just the body too — ChapterFileService.ReplaceBody folds it back in under
    /// CurrentText's existing front matter block, left untouched, rather than overwriting the
    /// whole file with body-only text and losing the front matter entirely.
    /// </summary>
    private void OnWysiwygMessageBody(string? body)
    {
        if (ViewModel is null || body is null)
            return;

        try
        {
            using var message = JsonDocument.Parse(body);
            if (message.RootElement.GetProperty("event").GetString() != "change")
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
    /// exist yet), copies it in if it was picked from elsewhere, and inserts it as a real HTML
    /// &lt;figure&gt; grouping the image with a ".caption"-styled &lt;figcaption&gt; underneath
    /// — see EditorStyleCatalog.
    /// </summary>
    private async void OnInsertImageClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(StorageProvider, ViewModel.CurrentProject.ImagesDir, "Insert Image");
        if (fileName is null)
            return;

        var altText = System.Net.WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(fileName));
        var html = $"""
            <figure>
            <img src="../images/{fileName}" alt="{altText}">
            <figcaption class="caption">Caption text</figcaption>
            </figure>
            """;

        InsertAtCursor(html);
    }

    private void OnEditorContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        var hasSelection = EditorTextBox.SelectionLength > 0;
        ApplyStyleMenuItem.IsEnabled = hasSelection;
        if (!hasSelection)
            return;

        ApplyStyleMenuItem.Items.Clear();
        foreach (var style in EditorStyleCatalog.Styles)
        {
            var item = new MenuItem { Header = style.Label, Tag = style };
            item.Click += OnApplyStyleClick;
            ApplyStyleMenuItem.Items.Add(item);
        }
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

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open eBook Editor Project",
            AllowMultiple = false
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path)
            return;

        try
        {
            var project = new ProjectService().LoadProject(path);
            OpenProjectInNewWindow(project);
        }
        catch (Exception ex)
        {
            if (ViewModel is not null)
                ViewModel.StatusMessage = $"Couldn't open project at {path}: {ex.Message}";
        }
    }

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

    private void OpenRecentProject(string path)
    {
        try
        {
            var project = new ProjectService().LoadProject(path);
            OpenProjectInNewWindow(project);
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

        ViewModel.ApplyAutofillDefaultsIfEmpty();
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

        ViewModel.ApplyAutofillDefaultsIfEmpty();
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

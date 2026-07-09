using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.App.Views;

public partial class MainWindow : Window
{
    private const string SpineItemIdFormat = "application/x-ebookeditor-spine-item-id";
    private const double DragThreshold = 6;

    private bool _suppressTextChanged;
    private bool _forceClose;
    private SpineItem? _dragCandidate;
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;
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
        else if (e.PropertyName == nameof(EditorViewModel.Mode) && ViewModel?.Editor.IsEditMode == true)
        {
            // AvaloniaEdit's TextView only invalidates the visual lines that overlap a text
            // change; while IsEditMode is false (IsVisible=False), the editor keeps whatever
            // visual lines it last built (possibly none, or a previous chapter's), so a chapter
            // loaded while hidden in Preview mode can come up blank the moment it's shown here.
            // Force-reassigning Document.Text (even though it's unchanged) drives the same full
            // replace/rebuild path a real edit takes, which reliably rebuilds the visual line
            // cache from scratch regardless of what state it was left in.
            _suppressTextChanged = true;
            EditorTextBox.Document.Text = ViewModel!.Editor.CurrentText;
            _suppressTextChanged = false;
            RedrawAndFocusEditorAfterLayout();
        }
    }

    /// <summary>
    /// Just-flipped-visible AvaloniaEdit still has whatever Bounds it had while hidden
    /// (possibly zero) at the exact moment the Mode/CurrentText PropertyChanged handler runs —
    /// IsVisible flipping True on the ViewModel side doesn't mean Avalonia has actually measured
    /// and arranged the control with its real size yet; that happens on the next layout pass.
    /// Calling TextView.Redraw() synchronously, before that pass has run, can rebuild the visual
    /// line cache against a stale/zero viewport and still show nothing. Posting to the
    /// dispatcher defers the redraw to a later turn of the UI message loop, after layout has
    /// caught up.
    /// </summary>
    private void RedrawAndFocusEditorAfterLayout()
    {
        Dispatcher.UIThread.Post(() =>
        {
            EditorTextBox.TextArea.TextView.Redraw();
            EditorTextBox.Focus();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Pushes CurrentText into the AvaloniaEdit control. Skipping the reassignment when the
    /// text already matches is essential, not just an optimization: every keystroke round-trips
    /// through OnEditorTextBoxTextChanged -> ViewModel.CurrentText -> this method, and
    /// unconditionally setting TextEditor.Text (even to its own current value) resets the
    /// caret/selection on every character typed, which made typing into the editor effectively
    /// impossible.
    /// </summary>
    private void SyncEditorTextFromViewModel()
    {
        if (ViewModel is null)
            return;

        var text = ViewModel.Editor.CurrentText;
        if (EditorTextBox.Text == text)
            return;

        _suppressTextChanged = true;
        EditorTextBox.Text = text;
        _suppressTextChanged = false;

        // Switching directly from one chapter to another while already in Edit mode changes
        // CurrentText without ever changing Mode, so it never reaches the Mode-changed redraw
        // in OnEditorViewModelPropertyChanged — leaving the editor showing stale/blank visual
        // lines from whichever chapter was displayed before, for the same virtualized-editor
        // staleness reason documented there. Redraw here whenever new text is actually applied.
        if (ViewModel.Editor.IsEditMode)
            RedrawAndFocusEditorAfterLayout();
    }

    private async void OnInsertTableClick(object? sender, RoutedEventArgs e)
    {
        var window = new InsertTableWindow();
        await window.ShowDialog(this);

        if (window.Result is not { } markdown)
            return;

        var offset = EditorTextBox.CaretOffset;
        EditorTextBox.Document.Insert(offset, markdown);
        EditorTextBox.CaretOffset = offset + markdown.Length;
        EditorTextBox.Focus();
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
            var item = new MenuItem { Header = style.Label, Tag = style.ClassName };
            item.Click += OnApplyStyleClick;
            ApplyStyleMenuItem.Items.Add(item);
        }
    }

    /// <summary>
    /// Wraps the current selection in a Markdown custom container ("::: {.class} ... :::"),
    /// naming the CSS class the chosen style hooks to in DefaultStylesheet.cs/"Vellum
    /// Serif.css". Markdig renders this as &lt;div class="…"&gt; in the EPUB; PDF/Word just
    /// render the wrapped content plainly, since neither has a stylesheet to consult.
    /// </summary>
    private void OnApplyStyleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string className })
            return;

        var selectionStart = EditorTextBox.SelectionStart;
        var selectionLength = EditorTextBox.SelectionLength;
        if (selectionLength <= 0)
            return;

        var selectedText = EditorTextBox.Document.GetText(selectionStart, selectionLength);
        var wrapped = $"::: {{.{className}}}\n{selectedText}\n:::";

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
                new FilePickerFileType("Chapter files") { Patterns = ["*.md", "*.docx", "*.html", "*.htm"] }
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

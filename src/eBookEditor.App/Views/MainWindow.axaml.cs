using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;

namespace eBookEditor.App.Views;

public partial class MainWindow : Window
{
    private const string SpineItemIdFormat = "application/x-ebookeditor-spine-item-id";
    private const double DragThreshold = 6;

    private bool _suppressTextChanged;
    private SpineItem? _dragCandidate;
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        EditorTextBox.TextChanged += OnEditorTextBoxTextChanged;
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel?.Editor.IsDirty == true)
            ViewModel.Editor.Save();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.Editor.PropertyChanged += OnEditorViewModelPropertyChanged;
        SyncEditorTextFromViewModel();
    }

    private void OnEditorViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.CurrentText) && !_suppressTextChanged)
            SyncEditorTextFromViewModel();
    }

    private void SyncEditorTextFromViewModel()
    {
        if (ViewModel is null)
            return;

        _suppressTextChanged = true;
        EditorTextBox.Text = ViewModel.Editor.CurrentText;
        _suppressTextChanged = false;
    }

    private void OnEditorTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged || ViewModel is null)
            return;

        ViewModel.Editor.CurrentText = EditorTextBox.Text ?? string.Empty;
    }

    private async void OnEditMetadataClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.ApplyAutofillDefaultsIfEmpty();
        var dialog = new MetadataEditorWindow(ViewModel);
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

    private void OnSpineItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || SpineListBox.SelectedItem is not SpineItem item)
            return;

        ViewModel.OpenSpineItem(item);
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

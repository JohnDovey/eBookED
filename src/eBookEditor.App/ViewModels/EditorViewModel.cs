using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace eBookEditor.App.ViewModels;

public enum EditorMode
{
    Edit,
    Preview
}

public partial class EditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentText = string.Empty;

    [ObservableProperty]
    private string _previewSource = string.Empty;

    [ObservableProperty]
    private EditorMode _mode = EditorMode.Edit;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _filePath;

    public bool IsEditMode => Mode == EditorMode.Edit;
    public bool IsPreviewMode => Mode == EditorMode.Preview;
    public string ModeLabel => IsEditMode ? "Edit" : "Preview";

    partial void OnModeChanged(EditorMode value)
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(ModeLabel));
    }

    partial void OnCurrentTextChanged(string value) => IsDirty = true;

    /// <summary>
    /// Only refreshes PreviewSource (which drives the Markdown.Avalonia render) when actually
    /// switching into Preview mode, not on every keystroke — mirrors the MarkDownED reference
    /// app's whole-pane toggle pattern.
    /// </summary>
    [RelayCommand]
    private void TogglePreview()
    {
        if (Mode == EditorMode.Edit)
        {
            PreviewSource = CurrentText;
            Mode = EditorMode.Preview;
        }
        else
        {
            Mode = EditorMode.Edit;
        }
    }

    /// <summary>
    /// Loading a new file preserves whatever Edit/Preview mode is currently active (so
    /// switching between chapters doesn't keep resetting the user's toggle choice) unless
    /// <paramref name="forcePreviewMode"/> is set — used for generated pages (title/copyright/
    /// TOC/about-author), which always open in Preview since hand-editing them gets clobbered
    /// by the next regenerate anyway.
    /// </summary>
    public void LoadFile(string path, bool forcePreviewMode = false)
    {
        FilePath = path;
        CurrentText = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        IsDirty = false;

        if (forcePreviewMode)
            Mode = EditorMode.Preview;

        if (Mode == EditorMode.Preview)
            PreviewSource = CurrentText;
    }

    public void Save()
    {
        if (FilePath is null)
            return;

        File.WriteAllText(FilePath, CurrentText);
        IsDirty = false;
    }
}

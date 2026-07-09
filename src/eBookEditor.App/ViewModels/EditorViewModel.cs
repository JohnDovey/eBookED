using CommunityToolkit.Mvvm.ComponentModel;

namespace eBookEditor.App.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentText = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _filePath;

    partial void OnCurrentTextChanged(string value) => IsDirty = true;

    public void LoadFile(string path)
    {
        FilePath = path;
        CurrentText = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        IsDirty = false;
    }

    public void Save()
    {
        if (FilePath is null)
            return;

        File.WriteAllText(FilePath, CurrentText);
        IsDirty = false;
    }
}

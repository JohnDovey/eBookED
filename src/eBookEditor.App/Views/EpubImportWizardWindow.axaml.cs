using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.EpubImport.Services;

namespace eBookEditor.App.Views;

public partial class EpubImportWizardWindow : Window
{
    private readonly string _epubPath;
    private readonly EpubProjectImporter _importer = new();

    public EpubImportWizardViewModel ViewModel { get; } = new();
    public EbookProject? CreatedProject { get; private set; }

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public EpubImportWizardWindow()
    {
        _epubPath = "";
        DataContext = ViewModel;
        InitializeComponent();
    }

    public EpubImportWizardWindow(string epubPath) : this()
    {
        _epubPath = epubPath;
        ViewModel.SourceDescription = $"From: {Path.GetFileName(epubPath)}";

        // Parsed eagerly (cheap relative to project creation) so the Project Name field starts
        // pre-filled from the EPUB's real dc:title rather than just its file name — the user
        // can still edit it before confirming.
        try
        {
            ViewModel.ProjectName = _importer.SuggestProjectName(epubPath);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = $"Could not read this EPUB: {ex.Message}";
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose Project Location",
            AllowMultiple = false
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            ViewModel.Location = path;
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ProjectName) || string.IsNullOrWhiteSpace(ViewModel.Location))
        {
            ViewModel.ErrorMessage = "A book title and save location are both required.";
            return;
        }

        try
        {
            CreatedProject = _importer.Import(_epubPath, ViewModel.Location, ViewModel.ProjectName.Trim());
            Close();
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}

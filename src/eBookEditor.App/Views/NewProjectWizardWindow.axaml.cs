using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.App.Views;

public partial class NewProjectWizardWindow : Window
{
    private readonly ProjectService _projectService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();

    public NewProjectWizardViewModel ViewModel { get; } = new();
    public EbookProject? CreatedProject { get; private set; }

    public NewProjectWizardWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
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
            var authorName = ViewModel.AuthorName.Trim();
            var metadata = new BookMetadata
            {
                Title = ViewModel.ProjectName.Trim(),
                Contributors = string.IsNullOrWhiteSpace(authorName)
                    ? []
                    : [new Contributor(authorName, ContributorRole.Author)],
                CopyrightHolder = authorName,
                CopyrightYear = DateTime.UtcNow.Year,
                Language = "en"
            };

            var project = _projectService.CreateProject(ViewModel.Location, ViewModel.ProjectName, metadata);
            _pageGenerator.RegenerateAllGeneratedPages(project);
            File.WriteAllText(project.BookMdPath, _bookIndexGenerator.GenerateBookMd(project));

            CreatedProject = project;
            Close();
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using eBookEditor.Core.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// Shown in place of a project window when there's no project open — currently reachable only
/// via "Close Project" (see MainWindow.OnCloseProjectClick), which opens one of these before
/// closing the project window it was called from, so the app keeps running (ShutdownMode is
/// OnLastWindowClose, so as long as this stays open the app doesn't quit) rather than exiting
/// the way closing the last project window normally would. Reuses MainWindow's own
/// OpenProjectInNewWindow/BuildMissingSpineItemsMessage helpers rather than duplicating them.
/// </summary>
public partial class WelcomeWindow : Window
{
    private readonly AppSettingsService _appSettingsService = new(new AppPaths());

    public WelcomeWindow()
    {
        InitializeComponent();
        Opened += (_, _) => PopulateRecentProjects();
    }

    private void PopulateRecentProjects()
    {
        RecentProjectsPanel.Children.Clear();

        var recentPaths = _appSettingsService.Load().RecentProjectPaths;
        if (recentPaths.Count == 0)
        {
            RecentProjectsPanel.Children.Add(new TextBlock { Text = "(No recent projects)", Opacity = 0.6 });
            return;
        }

        foreach (var path in recentPaths)
        {
            var button = new Button
            {
                Content = path,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += async (_, _) => await OpenProjectAtPathAsync(path);
            RecentProjectsPanel.Children.Add(button);
        }
    }

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        var wizard = new NewProjectWizardWindow();
        await wizard.ShowDialog(this);

        if (wizard.CreatedProject is { } project)
        {
            MainWindow.OpenProjectInNewWindow(project);
            Close();
        }
    }

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

        await OpenProjectAtPathAsync(path);
    }

    private async Task OpenProjectAtPathAsync(string path)
    {
        try
        {
            var result = new ProjectService().LoadProject(path);
            MainWindow.OpenProjectInNewWindow(result.Project);

            if (result.MissingSpineItemPaths.Count > 0)
                await new MessageWindow("Open Project", MainWindow.BuildMissingSpineItemsMessage(result.MissingSpineItemPaths)).ShowDialog(this);

            Close();
        }
        catch (Exception ex)
        {
            await new MessageWindow("Open Project", $"Couldn't open a project at:\n{path}\n\n{ex.Message}").ShowDialog(this);
        }
    }
}

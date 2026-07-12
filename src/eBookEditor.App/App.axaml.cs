using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using eBookEditor.App.Services;
using eBookEditor.App.ViewModels;
using eBookEditor.App.Views;
using eBookEditor.Core.Services;
#if DEBUG
using Avalonia.Diagnostics;
#endif

namespace eBookEditor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Each project window is independent (see MainWindow's New/Open Project
            // handlers, which Show() additional windows rather than swapping the current
            // one's project) — don't quit just because one of several windows closed.
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var appSettingsService = new AppSettingsService(new AppPaths());
            var windows = RestoreLastSessionWindows(appSettingsService);

            desktop.MainWindow = windows[0];
            foreach (var window in windows.Skip(1))
                window.Show();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Reopens whichever projects were still open when the app last closed. Falls back to
    /// the most recently opened project if none were open (e.g. the user quit via each
    /// window's own close button), and only bootstraps the auto-generated sample project if
    /// there's no project history at all — first run only.
    /// </summary>
    private static IReadOnlyList<MainWindow> RestoreLastSessionWindows(AppSettingsService appSettingsService)
    {
        var projectService = new ProjectService();
        var settings = appSettingsService.Load();
        var pathsToRestore = settings.OpenProjectPaths.Count > 0
            ? settings.OpenProjectPaths
            : settings.RecentProjectPaths.Take(1).ToList();

        var windows = new List<MainWindow>();
        foreach (var path in pathsToRestore)
        {
            if (!Directory.Exists(path))
                continue;

            try
            {
                var result = projectService.LoadProject(path);
                var viewModel = new MainWindowViewModel(result.Project, appSettingsService);
                if (result.MissingSpineItemPaths.Count > 0)
                    viewModel.StatusMessage = $"{result.MissingSpineItemPaths.Count} file(s) referenced in this project were missing on disk and have been excluded: {string.Join(", ", result.MissingSpineItemPaths)}";
                windows.Add(new MainWindow { DataContext = viewModel });
            }
            catch
            {
                // Skip projects that no longer load (moved/deleted/corrupted) rather than blocking startup.
            }
        }

        if (windows.Count == 0)
        {
            var project = SampleProjectFactory.LoadOrCreate(AppContext.BaseDirectory);
            windows.Add(new MainWindow { DataContext = new MainWindowViewModel(project, appSettingsService) });
        }

        return windows;
    }
}

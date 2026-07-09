using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using eBookEditor.App.Services;
using eBookEditor.App.ViewModels;
using eBookEditor.App.Views;
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

            var project = SampleProjectFactory.LoadOrCreate(AppContext.BaseDirectory);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(project)
            };

#if DEBUG
            this.AttachDevTools();
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }
}

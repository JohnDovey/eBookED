using Avalonia;
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

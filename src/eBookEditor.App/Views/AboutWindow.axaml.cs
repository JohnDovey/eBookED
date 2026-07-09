using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "" : $"Version {version.ToString(3)}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

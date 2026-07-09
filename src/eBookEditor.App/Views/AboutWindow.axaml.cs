using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

public partial class AboutWindow : Window
{
    private const string ContactEmail = "jdovey.john@gamail.com";

    private readonly string _versionString;

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _versionString = version is null ? "" : version.ToString(3);
        VersionText.Text = _versionString.Length == 0 ? "" : $"Version {_versionString}";
        ContactText.Text = $"© {DateTime.UtcNow.Year} John Dovey — {ContactEmail}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnContactPressed(object? sender, PointerPressedEventArgs e)
    {
        var subject = Uri.EscapeDataString($"eBook Editor {_versionString}".Trim());
        var mailto = $"mailto:{ContactEmail}?subject={subject}";
        Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
    }
}

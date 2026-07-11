using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

/// <summary>A minimal single-message/OK dialog — for short informational messages (e.g. "Insert
/// Internal Link"'s "no destinations exist yet" case) that don't need a full custom window of
/// their own.</summary>
public partial class MessageWindow : Window
{
    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public MessageWindow() : this("", "")
    {
    }

    public MessageWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}

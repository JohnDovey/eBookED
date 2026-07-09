using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message, string confirmLabel) : this()
    {
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
}

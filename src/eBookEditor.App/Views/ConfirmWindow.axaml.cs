using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

public enum ConfirmResult
{
    Cancel,
    No,
    Yes
}

/// <summary>Generic Yes/No/Cancel confirm dialog — same Window+Close(result) shape as
/// UnsavedChangesDialog, just with different labels/result values. First use: MainWindowViewModel
/// offering to regenerate the Index/List of Figures before an export that might otherwise ship a
/// stale one.</summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow() : this("Confirm", "")
    {
    }

    public ConfirmWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(ConfirmResult.Cancel);

    private void OnNoClick(object? sender, RoutedEventArgs e) => Close(ConfirmResult.No);

    private void OnYesClick(object? sender, RoutedEventArgs e) => Close(ConfirmResult.Yes);
}

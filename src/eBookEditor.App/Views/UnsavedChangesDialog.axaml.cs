using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

public enum UnsavedChangesResult
{
    Cancel,
    Discard,
    Save
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Cancel);

    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Discard);

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Save);
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

/// <summary>A single multi-line text box for a footnote's note text. Insert sets
/// <see cref="Result"/> to the trimmed text (null on Cancel, or if left blank).</summary>
public partial class InsertFootnoteWindow : Window
{
    public string? Result { get; private set; }

    public InsertFootnoteWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        var text = NoteText.Text?.Trim();
        Result = string.IsNullOrWhiteSpace(text) ? null : text;
        Close();
    }
}

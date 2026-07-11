using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

/// <summary>A term field plus "mark all occurrences" checkbox for "Mark as Index Entry…".
/// Mark sets <see cref="Result"/> (null on Cancel, or if the term is left blank).</summary>
public partial class MarkIndexEntryWindow : Window
{
    public (string Term, bool MarkAllOccurrences)? Result { get; private set; }

    public MarkIndexEntryWindow() : this(null)
    {
    }

    public MarkIndexEntryWindow(string? initialTerm)
    {
        InitializeComponent();
        TermText.Text = initialTerm;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnMarkClick(object? sender, RoutedEventArgs e)
    {
        var term = TermText.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            Result = (term, MarkAllOccurrencesCheckBox.IsChecked == true);
        Close();
    }
}

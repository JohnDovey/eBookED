using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eBookEditor.App.Views;

/// <summary>A single label field for "Mark Link Destination" — the label only feeds the
/// generated dest: slug (see MainWindow.OnMarkLinkDestinationClick), it does not replace or
/// require any actual selected text; the caller wraps whatever's currently selected regardless
/// of what's typed here. Mark sets <see cref="Result"/> to the trimmed label (null on Cancel,
/// or if left blank).</summary>
public partial class MarkLinkDestinationWindow : Window
{
    public string? Result { get; private set; }

    public MarkLinkDestinationWindow() : this(null)
    {
    }

    public MarkLinkDestinationWindow(string? initialLabel)
    {
        InitializeComponent();
        LabelText.Text = initialLabel;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnMarkClick(object? sender, RoutedEventArgs e)
    {
        var text = LabelText.Text?.Trim();
        Result = string.IsNullOrWhiteSpace(text) ? null : text;
        Close();
    }
}

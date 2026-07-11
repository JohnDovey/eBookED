using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// Width/height (proportionally linked — editing one recomputes the other from the natural
/// aspect ratio), alignment, and "flow text around image" for "Insert Image…" — see
/// ImagePlacement for how these become the generated &lt;figure&gt;'s own size/style attributes.
/// Flow is force-unchecked and disabled whenever Center is selected, since a centered block has
/// no side for text to flow to.
/// </summary>
public partial class InsertImageWindow : Window
{
    private readonly double _aspectRatio;
    private bool _suppressSizeSync;

    public (int Width, int Height, ImageAlignment Alignment, bool Flow)? Result { get; private set; }

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public InsertImageWindow() : this(400, 300)
    {
    }

    public InsertImageWindow(int naturalWidth, int naturalHeight)
    {
        InitializeComponent();
        _aspectRatio = naturalHeight / (double)naturalWidth;

        _suppressSizeSync = true;
        WidthUpDown.Value = naturalWidth;
        HeightUpDown.Value = naturalHeight;
        _suppressSizeSync = false;
    }

    private void OnWidthChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressSizeSync || WidthUpDown.Value is not { } width)
            return;

        _suppressSizeSync = true;
        HeightUpDown.Value = Math.Max(1, Math.Round(width * (decimal)_aspectRatio));
        _suppressSizeSync = false;
    }

    private void OnHeightChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressSizeSync || HeightUpDown.Value is not { } height || _aspectRatio == 0)
            return;

        _suppressSizeSync = true;
        WidthUpDown.Value = Math.Max(1, Math.Round(height / (decimal)_aspectRatio));
        _suppressSizeSync = false;
    }

    private void OnAlignmentChanged(object? sender, RoutedEventArgs e)
    {
        if (CenterRadio.IsChecked == true)
        {
            FlowCheckBox.IsChecked = false;
            FlowCheckBox.IsEnabled = false;
        }
        else
        {
            FlowCheckBox.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        var alignment = RightRadio.IsChecked == true ? ImageAlignment.Right
            : LeftRadio.IsChecked == true ? ImageAlignment.Left
            : ImageAlignment.Center;

        var width = (int)(WidthUpDown.Value ?? 400);
        var height = (int)(HeightUpDown.Value ?? 300);
        var flow = FlowCheckBox.IsChecked == true && alignment != ImageAlignment.Center;

        Result = (width, height, alignment, flow);
        Close();
    }
}

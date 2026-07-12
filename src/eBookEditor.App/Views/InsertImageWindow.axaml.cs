using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// Width/height (proportionally linked — editing one recomputes the other from the natural
/// aspect ratio, then both are clamped to fit the project's chosen PDF page size minus its
/// printable margin, on whichever axis is tighter), alignment, "flow text around image", and a
/// caption for "Insert Image…" — see ImagePlacement for how size/alignment/flow become the
/// generated &lt;figure&gt;'s own attributes/style. Flow is force-unchecked and disabled whenever
/// Center is selected, since a centered block has no side for text to flow to.
/// </summary>
public partial class InsertImageWindow : Window
{
    /// <summary>Matches PdfBuilder's own hardcoded page margin, so the clamp reflects the same
    /// printable area the PDF export will actually use.</summary>
    private const double MarginInches = 0.75;

    /// <summary>CSS/HTML's standard reference pixel density — the same assumption every other
    /// width/height attribute this app writes into generated HTML already relies on.</summary>
    private const double PixelsPerInch = 96;

    private readonly double _aspectRatio;
    private readonly decimal _maxWidthPx;
    private readonly decimal _maxHeightPx;
    private bool _suppressSizeSync;

    public (int Width, int Height, ImageAlignment Alignment, bool Flow, string Caption)? Result { get; private set; }

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public InsertImageWindow() : this(400, 300, PdfPageSizeCatalog.Resolve(null), "")
    {
    }

    public InsertImageWindow(int naturalWidth, int naturalHeight, PdfPageSizeOption pageSize, string defaultCaption)
    {
        InitializeComponent();
        _aspectRatio = naturalHeight / (double)naturalWidth;
        _maxWidthPx = (decimal)Math.Max(1, (pageSize.WidthInches - 2 * MarginInches) * PixelsPerInch);
        _maxHeightPx = (decimal)Math.Max(1, (pageSize.HeightInches - 2 * MarginInches) * PixelsPerInch);
        WidthUpDown.Maximum = _maxWidthPx;
        HeightUpDown.Maximum = _maxHeightPx;
        PageFitNoticeText.Text = $"Limited to {_maxWidthPx:0} × {_maxHeightPx:0}px to fit the {pageSize.Name} page.";

        _suppressSizeSync = true;
        var (width, height) = ClampFromWidth(naturalWidth);
        WidthUpDown.Value = width;
        HeightUpDown.Value = height;
        _suppressSizeSync = false;

        CaptionTextBox.Text = defaultCaption;
    }

    private (decimal Width, decimal Height) ClampFromWidth(decimal proposedWidth)
    {
        var width = Math.Min(Math.Max(1, proposedWidth), _maxWidthPx);
        var height = Math.Max(1, Math.Round(width * (decimal)_aspectRatio));
        if (height > _maxHeightPx)
        {
            height = _maxHeightPx;
            width = _aspectRatio == 0 ? width : Math.Max(1, Math.Round(height / (decimal)_aspectRatio));
        }

        return (width, height);
    }

    private (decimal Width, decimal Height) ClampFromHeight(decimal proposedHeight)
    {
        var height = Math.Min(Math.Max(1, proposedHeight), _maxHeightPx);
        var width = _aspectRatio == 0 ? height : Math.Max(1, Math.Round(height / (decimal)_aspectRatio));
        if (width > _maxWidthPx)
        {
            width = _maxWidthPx;
            height = Math.Max(1, Math.Round(width * (decimal)_aspectRatio));
        }

        return (width, height);
    }

    private void OnWidthChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressSizeSync || WidthUpDown.Value is not { } width)
            return;

        _suppressSizeSync = true;
        var (clampedWidth, height) = ClampFromWidth(width);
        WidthUpDown.Value = clampedWidth;
        HeightUpDown.Value = height;
        _suppressSizeSync = false;
    }

    private void OnHeightChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_suppressSizeSync || HeightUpDown.Value is not { } height || _aspectRatio == 0)
            return;

        _suppressSizeSync = true;
        var (width, clampedHeight) = ClampFromHeight(height);
        WidthUpDown.Value = width;
        HeightUpDown.Value = clampedHeight;
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
        var caption = CaptionTextBox.Text ?? "";

        Result = (width, height, alignment, flow, caption);
        Close();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.Services;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// Two usage modes:
/// - "Insert new" (InsertImageWindow(imagesDir, pageSize)) — nothing picked yet. The Gallery
///   checkbox and a picker button let the user choose either a single image (seeded into the
///   width/height/alignment/flow/caption fields below) or, checked, up to
///   GalleryHtmlBuilder.MaxImages images for a gallery table (see MainWindow.OnInsertImageClick/
///   GalleryHtmlBuilder).
/// - "Edit existing figure" (InsertImageWindow(naturalWidth, naturalHeight, ...)) — right-click
///   "Edit Image…" (see MainWindow.OnEditFigureClick) on an already-inserted single image.
///   Width/height/alignment/flow/caption are already known, so this skips straight to the
///   editable fields with no file-picking UI and no Gallery option — editing a whole gallery
///   happens per-image, by right-clicking that one figure, not through this window.
///
/// Width/height are proportionally linked (editing one recomputes the other from the natural
/// aspect ratio), then clamped to fit the project's chosen PDF page size minus its printable
/// margin, on whichever axis is tighter — see ImagePlacement for how size/alignment/flow become
/// the generated &lt;figure&gt;'s own attributes/style. Flow is force-unchecked and disabled
/// whenever Center is selected, since a centered block has no side for text to flow to.
/// </summary>
public partial class InsertImageWindow : Window
{
    /// <summary>Matches PdfBuilder's own hardcoded page margin, so the clamp reflects the same
    /// printable area the PDF export will actually use.</summary>
    private const double MarginInches = 0.75;

    /// <summary>CSS/HTML's standard reference pixel density — the same assumption every other
    /// width/height attribute this app writes into generated HTML already relies on.</summary>
    private const double PixelsPerInch = 96;

    private readonly string? _imagesDir;
    private double _aspectRatio;
    private decimal _maxWidthPx;
    private decimal _maxHeightPx;
    private bool _suppressSizeSync;
    private string? _singleFileName;

    public (int Width, int Height, ImageAlignment Alignment, bool Flow, string Caption)? Result { get; private set; }

    /// <summary>Only set in "insert new single image" mode — null when editing an existing
    /// figure (the caller already knows which file it is) or when a gallery was inserted
    /// instead (see GalleryResult).</summary>
    public string? SelectedFileName { get; private set; }

    public IReadOnlyList<GalleryImageSelection>? GalleryResult { get; private set; }

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public InsertImageWindow() : this("", PdfPageSizeCatalog.Resolve(null))
    {
    }

    /// <summary>"Insert new image(s)" mode.</summary>
    public InsertImageWindow(string imagesDir, PdfPageSizeOption pageSize)
    {
        InitializeComponent();
        _imagesDir = imagesDir;
        ConfigurePageFitLimits(pageSize);
    }

    /// <summary>"Edit existing figure" mode. initialAlignment/initialFlow let right-click "Edit
    /// Image…" reopen this dialog pre-filled with an existing figure's current placement,
    /// rather than always defaulting to Center/no-flow the way a brand new "Insert Image…"
    /// does.</summary>
    public InsertImageWindow(
        int naturalWidth, int naturalHeight, PdfPageSizeOption pageSize, string defaultCaption,
        ImageAlignment initialAlignment = ImageAlignment.Center, bool initialFlow = false,
        string title = "Insert Image")
    {
        InitializeComponent();
        Title = title;
        ConfigurePageFitLimits(pageSize);

        GalleryCheckBox.IsVisible = false;
        PickImagesButton.IsVisible = false;
        InsertButton.IsEnabled = true;

        SeedSingleImageFields(naturalWidth, naturalHeight, defaultCaption, initialAlignment, initialFlow);
        SingleImagePanel.IsVisible = true;
    }

    private void ConfigurePageFitLimits(PdfPageSizeOption pageSize)
    {
        _maxWidthPx = (decimal)Math.Max(1, (pageSize.WidthInches - 2 * MarginInches) * PixelsPerInch);
        _maxHeightPx = (decimal)Math.Max(1, (pageSize.HeightInches - 2 * MarginInches) * PixelsPerInch);
        WidthUpDown.Maximum = _maxWidthPx;
        HeightUpDown.Maximum = _maxHeightPx;
        PageFitNoticeText.Text = $"Limited to {_maxWidthPx:0} × {_maxHeightPx:0}px to fit the {pageSize.Name} page.";
    }

    private void SeedSingleImageFields(int naturalWidth, int naturalHeight, string defaultCaption, ImageAlignment initialAlignment, bool initialFlow)
    {
        _aspectRatio = naturalHeight / (double)naturalWidth;

        _suppressSizeSync = true;
        var (width, height) = ClampFromWidth(naturalWidth);
        WidthUpDown.Value = width;
        HeightUpDown.Value = height;
        _suppressSizeSync = false;

        CaptionTextBox.Text = defaultCaption;

        (initialAlignment switch
        {
            ImageAlignment.Left => LeftRadio,
            ImageAlignment.Right => RightRadio,
            _ => CenterRadio,
        }).IsChecked = true;
        FlowCheckBox.IsChecked = initialFlow && initialAlignment != ImageAlignment.Center;
    }

    /// <summary>Switching modes discards whatever was already picked, rather than trying to
    /// carry a single picked file into gallery mode (or vice versa) — simplest, least
    /// surprising behavior, and re-picking is one click away either way.</summary>
    private void OnGalleryCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (PickImagesButton is null)
            return; // fires once during InitializeComponent, before the rest of the tree exists

        PickImagesButton.Content = GalleryCheckBox.IsChecked == true ? "Choose Images…" : "Choose Image…";
        _singleFileName = null;
        GalleryResult = null;
        SingleImagePanel.IsVisible = false;
        GallerySummaryBorder.IsVisible = false;
        InsertButton.IsEnabled = false;
    }

    private async void OnPickImagesButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_imagesDir is not { Length: > 0 })
            return;

        if (GalleryCheckBox.IsChecked == true)
        {
            var fileNames = await ProjectImagePicker.PickAndCopyMultipleIntoImagesDirAsync(
                StorageProvider, _imagesDir, "Insert Gallery Images", GalleryHtmlBuilder.MaxImages);
            if (fileNames.Count == 0)
                return;

            var selections = fileNames.Select(fileName =>
            {
                var (naturalWidth, naturalHeight) = TryReadPixelSize(Path.Combine(_imagesDir, fileName)) ?? (400, 300);
                return new GalleryImageSelection(fileName, naturalWidth, naturalHeight, Path.GetFileNameWithoutExtension(fileName));
            }).ToList();

            GalleryResult = selections;
            GallerySummaryText.Text = $"{selections.Count} image(s) selected:\n" + string.Join("\n", selections.Select(s => s.FileName));
            GallerySummaryBorder.IsVisible = true;
            SingleImagePanel.IsVisible = false;
            InsertButton.IsEnabled = true;
        }
        else
        {
            var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(StorageProvider, _imagesDir, "Insert Image");
            if (fileName is null)
                return;

            var (naturalWidth, naturalHeight) = TryReadPixelSize(Path.Combine(_imagesDir, fileName)) ?? (400, 300);
            _singleFileName = fileName;
            SeedSingleImageFields(naturalWidth, naturalHeight, Path.GetFileNameWithoutExtension(fileName), ImageAlignment.Center, false);
            SingleImagePanel.IsVisible = true;
            GallerySummaryBorder.IsVisible = false;
            InsertButton.IsEnabled = true;
        }
    }

    private static (int Width, int Height)? TryReadPixelSize(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            return (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch
        {
            // Best-effort — falls back to a default size if the file can't be decoded (an
            // unsupported/corrupt format).
            return null;
        }
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
        if (GalleryResult is { Count: > 0 })
        {
            Close();
            return;
        }

        var alignment = RightRadio.IsChecked == true ? ImageAlignment.Right
            : LeftRadio.IsChecked == true ? ImageAlignment.Left
            : ImageAlignment.Center;

        var width = (int)(WidthUpDown.Value ?? 400);
        var height = (int)(HeightUpDown.Value ?? 300);
        var flow = FlowCheckBox.IsChecked == true && alignment != ImageAlignment.Center;
        var caption = CaptionTextBox.Text ?? "";

        Result = (width, height, alignment, flow, caption);
        SelectedFileName = _singleFileName;
        Close();
    }
}

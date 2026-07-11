using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.Core.Models;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

/// <summary>
/// Two-step chapter-then-destination picker for "Insert Internal Link" — the caller
/// (MainWindow.OnInsertInternalLinkClick) already confirmed at least one destination exists
/// before constructing this window (it shows its own "A Link Destination must be created
/// before you can link to it" message otherwise, rather than this window ever needing to
/// represent an empty state). Insert Link sets <see cref="Result"/> to the chosen destination
/// (null on Cancel, or if nothing is selected).
/// </summary>
public partial class InsertInternalLinkWindow : Window
{
    private sealed record ChapterOption(SpineItem Item)
    {
        public override string ToString() => Item.DisplayTitle;
    }

    private sealed record DestinationOption(LinkDestination Destination)
    {
        public override string ToString() => Destination.Label;
    }

    private readonly IReadOnlyList<LinkDestination> _destinations;

    public LinkDestination? Result { get; private set; }

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader.
    public InsertInternalLinkWindow() : this([])
    {
    }

    public InsertInternalLinkWindow(IReadOnlyList<LinkDestination> destinations)
    {
        InitializeComponent();
        _destinations = destinations;

        var chapters = destinations
            .GroupBy(d => d.Item.Id)
            .Select(g => new ChapterOption(g.First().Item))
            .ToList();

        ChapterList.ItemsSource = chapters;
        if (chapters.Count > 0)
            ChapterList.SelectedIndex = 0;
    }

    private void OnChapterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ChapterList.SelectedItem is not ChapterOption chapter)
        {
            DestinationList.ItemsSource = null;
            return;
        }

        DestinationList.ItemsSource = _destinations
            .Where(d => d.Item.Id == chapter.Item.Id)
            .Select(d => new DestinationOption(d))
            .ToList();
    }

    private void OnDestinationSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        InsertButton.IsEnabled = DestinationList.SelectedItem is DestinationOption;

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        if (DestinationList.SelectedItem is DestinationOption option)
            Result = option.Destination;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}

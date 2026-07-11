using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.Services;
using eBookEditor.App.ViewModels;

namespace eBookEditor.App.Views;

public partial class CopyrightPublishingWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel = null!;

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader,
    // which requires a public no-arg constructor to exist even though it's never invoked
    // at actual runtime — real usage always goes through the constructor below.
    public CopyrightPublishingWindow()
    {
        InitializeComponent();
    }

    public CopyrightPublishingWindow(MainWindowViewModel mainViewModel) : this()
    {
        _mainViewModel = mainViewModel;
        DataContext = mainViewModel.Metadata;
    }

    private async void OnBrowsePublisherLogoClick(object? sender, RoutedEventArgs e)
    {
        var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(
            StorageProvider, _mainViewModel.CurrentProject.ImagesDir, "Choose Publisher Logo");
        if (fileName is null)
            return;

        _mainViewModel.Metadata.PublisherLogoPath = $"images/{fileName}";
    }

    private async void OnBrowseCoverImageClick(object? sender, RoutedEventArgs e)
    {
        var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(
            StorageProvider, _mainViewModel.CurrentProject.ImagesDir, "Choose Cover Image");
        if (fileName is null)
            return;

        _mainViewModel.Metadata.CoverImagePath = $"images/{fileName}";
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _mainViewModel.SaveMetadataAndRegenerate();
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _mainViewModel.Metadata.LoadFrom(_mainViewModel.CurrentProject.Metadata);
        Close();
    }

    private void OnRemoveStoreLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: StoreLinkEntry entry })
            _mainViewModel.Metadata.StoreLinks.Remove(entry);
    }
}

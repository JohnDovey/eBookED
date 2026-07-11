using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.Services;
using eBookEditor.App.ViewModels;

namespace eBookEditor.App.Views;

public partial class AboutTheAuthorWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel = null!;

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader,
    // which requires a public no-arg constructor to exist even though it's never invoked
    // at actual runtime — real usage always goes through the constructor below.
    public AboutTheAuthorWindow()
    {
        InitializeComponent();
    }

    public AboutTheAuthorWindow(MainWindowViewModel mainViewModel) : this()
    {
        _mainViewModel = mainViewModel;
        DataContext = mainViewModel.Metadata;
    }

    /// <summary>
    /// Picks an image (starting in the project's images/ folder, created if it doesn't exist
    /// yet), copies it in if it was picked from elsewhere (see ProjectImagePicker — the same
    /// pick-and-copy behavior the editor's own Insert Image command uses), and points
    /// AuthorPhotoPath at the copy — project-root-relative, matching how
    /// PageGeneratorService.GenerateAboutAuthorPage resolves it ("../{photoPath}" from
    /// backmatter/about-the-author.ebhtml's own location).
    /// </summary>
    private async void OnBrowseAuthorPhotoClick(object? sender, RoutedEventArgs e)
    {
        var fileName = await ProjectImagePicker.PickAndCopyIntoImagesDirAsync(
            StorageProvider, _mainViewModel.CurrentProject.ImagesDir, "Choose Author Photo");
        if (fileName is null)
            return;

        _mainViewModel.Metadata.AuthorPhotoPath = $"images/{fileName}";
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

    private void OnRemoveSocialLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SocialLinkEntry entry })
            _mainViewModel.Metadata.SocialLinks.Remove(entry);
    }
}

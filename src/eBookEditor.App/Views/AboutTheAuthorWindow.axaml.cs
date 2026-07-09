using Avalonia.Controls;
using Avalonia.Interactivity;
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

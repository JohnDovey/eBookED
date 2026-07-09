using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.ViewModels;

namespace eBookEditor.App.Views;

public partial class GenreTagsWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel = null!;

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader,
    // which requires a public no-arg constructor to exist even though it's never invoked
    // at actual runtime — real usage always goes through the constructor below.
    public GenreTagsWindow()
    {
        InitializeComponent();
    }

    public GenreTagsWindow(MainWindowViewModel mainViewModel) : this()
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

    private void OnRemoveGenreTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TagEntry entry })
            _mainViewModel.Metadata.GenreTags.Remove(entry);
    }

    private void OnRemoveFreeTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TagEntry entry })
            _mainViewModel.Metadata.FreeTags.Remove(entry);
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.ViewModels;

namespace eBookEditor.App.Views;

public partial class MetadataEditorWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel = null!;

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader,
    // which requires a public no-arg constructor to exist even though it's never invoked
    // at actual runtime — real usage always goes through the constructor below.
    public MetadataEditorWindow()
    {
        InitializeComponent();
    }

    public MetadataEditorWindow(MainWindowViewModel mainViewModel) : this()
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

    private void OnTemplateDropDownOpened(object? sender, EventArgs e) => _mainViewModel.RefreshAvailableTemplates();

    private MetadataViewModel Metadata => _mainViewModel.Metadata;

    private void OnRemoveAuthorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ContributorEntry entry })
            Metadata.Authors.Remove(entry);
    }

    private void OnRemoveEditorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ContributorEntry entry })
            Metadata.Editors.Remove(entry);
    }

    private void OnRemoveIllustratorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ContributorEntry entry })
            Metadata.Illustrators.Remove(entry);
    }

    private void OnRemoveGenreTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TagEntry entry })
            Metadata.GenreTags.Remove(entry);
    }

    private void OnRemoveFreeTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TagEntry entry })
            Metadata.FreeTags.Remove(entry);
    }

    private void OnRemoveSocialLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SocialLinkEntry entry })
            Metadata.SocialLinks.Remove(entry);
    }

    private void OnRemoveStoreLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: StoreLinkEntry entry })
            Metadata.StoreLinks.Remove(entry);
    }
}

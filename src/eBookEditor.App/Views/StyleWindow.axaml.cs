using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.ViewModels;
using eBookEditor.Html.Services;

namespace eBookEditor.App.Views;

public partial class StyleWindow : Window
{
    private readonly MainWindowViewModel _mainViewModel = null!;

    // Parameterless constructor satisfies Avalonia's XAML runtime/design-time loader,
    // which requires a public no-arg constructor to exist even though it's never invoked
    // at actual runtime — real usage always goes through the constructor below.
    public StyleWindow()
    {
        InitializeComponent();
    }

    public StyleWindow(MainWindowViewModel mainViewModel) : this()
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

    private void OnTemplateSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        _mainViewModel.EnsureTemplateFontsInstalled(_mainViewModel.Metadata.SelectedTemplate);

    /// <summary>Renders the template picker's live (possibly unsaved) selection against a
    /// generated showcase document exercising every style the template targets — see
    /// TemplateShowcaseHtml — in a standalone Preview window, so the user can judge a
    /// candidate template before clicking Save &amp; Regenerate.</summary>
    private void OnPreviewTemplateClick(object? sender, RoutedEventArgs e)
    {
        var css = _mainViewModel.GetTemplateCss(_mainViewModel.Metadata.SelectedTemplate);
        var preview = new PreviewWindow();
        preview.Show(this);
        preview.UpdateContent(css, TemplateShowcaseHtml.Build(), $"Template Preview — {_mainViewModel.Metadata.SelectedTemplate}");
    }
}

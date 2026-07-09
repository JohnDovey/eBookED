using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using eBookEditor.App.ViewModels;

namespace eBookEditor.App.Views;

public partial class GenerationResultWindow : Window
{
    private string? _outputPath;

    public GenerationResultWindow()
    {
        InitializeComponent();
    }

    public GenerationResultWindow(GenerationResult result) : this()
    {
        Title = result.Success ? $"{result.FormatName} Export Complete" : $"{result.FormatName} Export Failed";
        HeadingText.Text = result.Success ? $"{result.FormatName} exported successfully" : $"{result.FormatName} export failed";

        if (result.Success)
        {
            _outputPath = result.OutputPath;
            DetailText.Text = result.OutputPath;

            var counts = new List<string>();
            if (result.WordCount is { } wordCount)
                counts.Add($"{wordCount:N0} words");
            if (result.PageCount is { } pageCount)
                counts.Add($"{pageCount:N0} pages");

            if (counts.Count > 0)
            {
                CountsText.Text = string.Join(" · ", counts);
                CountsText.IsVisible = true;
            }

            OpenFileButton.IsVisible = true;
            OpenFolderButton.IsVisible = true;
        }
        else
        {
            DetailText.Text = result.ErrorMessage;
        }
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (_outputPath is not null)
            Process.Start(new ProcessStartInfo(_outputPath) { UseShellExecute = true });
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_outputPath is not null && Path.GetDirectoryName(_outputPath) is { } directory)
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

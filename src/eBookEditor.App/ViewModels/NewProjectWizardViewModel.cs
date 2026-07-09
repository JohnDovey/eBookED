using CommunityToolkit.Mvvm.ComponentModel;

namespace eBookEditor.App.ViewModels;

public partial class NewProjectWizardViewModel : ViewModelBase
{
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _authorName = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
}

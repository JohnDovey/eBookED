using CommunityToolkit.Mvvm.ComponentModel;

namespace eBookEditor.App.ViewModels;

public partial class ContributorEntry : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _sortName = string.Empty;
}

public partial class TagEntry : ObservableObject
{
    [ObservableProperty] private string _value = string.Empty;
}

public partial class SocialLinkEntry : ObservableObject
{
    [ObservableProperty] private string _platform = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
}

public partial class StoreLinkEntry : ObservableObject
{
    [ObservableProperty] private string _store = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
}

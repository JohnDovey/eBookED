using CommunityToolkit.Mvvm.ComponentModel;

namespace eBookEditor.App.ViewModels;

public partial class ContributorEntry : ObservableObject
{
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;

    /// <summary>Splits a single "First Last" string on its last space, for autofilling from
    /// the app-level MRU name lists (which predate the first/last name split).</summary>
    public static ContributorEntry FromFullName(string fullName)
    {
        var trimmed = fullName.Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        return lastSpace < 0
            ? new ContributorEntry { FirstName = trimmed }
            : new ContributorEntry { FirstName = trimmed[..lastSpace].Trim(), LastName = trimmed[(lastSpace + 1)..].Trim() };
    }
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

namespace eBookEditor.Core.Models;

public enum ContributorRole
{
    Author,
    Editor,
    Illustrator,
    Translator,
    Foreword,
    Other
}

/// <summary>
/// Name is split into <paramref name="FirstName"/>/<paramref name="LastName"/> so the EPUB
/// "file-as" catalog/library sort form ("Dovey, John") can be derived automatically instead
/// of requiring a separately-maintained sort name.
/// </summary>
public record Contributor(string FirstName, string LastName, ContributorRole Role)
{
    public string Name => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}".Trim();

    public string SortName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : string.IsNullOrWhiteSpace(FirstName) ? LastName : $"{LastName}, {FirstName}";
}

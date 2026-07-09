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
/// <paramref name="SortName"/> is the EPUB "file-as" catalog/library sort form of the name
/// (e.g. "Dovey, John" for "John Dovey") — optional, falls back to <paramref name="Name"/>
/// when not set.
/// </summary>
public record Contributor(string Name, ContributorRole Role, string? SortName = null);

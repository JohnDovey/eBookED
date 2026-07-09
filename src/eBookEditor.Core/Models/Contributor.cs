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

public record Contributor(string Name, ContributorRole Role);

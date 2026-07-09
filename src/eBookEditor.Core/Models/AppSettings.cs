namespace eBookEditor.Core.Models;

public record AppSettings
{
    public List<string> RecentProjectPaths { get; init; } = new();
    public List<string> KnownAuthorNames { get; init; } = new();
    public List<string> KnownEditorNames { get; init; } = new();
    public List<string> KnownIllustratorNames { get; init; } = new();
    public List<PublisherInfo> KnownPublishers { get; init; } = new();
    public string DefaultLanguage { get; init; } = "en";
}

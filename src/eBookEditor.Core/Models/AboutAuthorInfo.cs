namespace eBookEditor.Core.Models;

public record AboutAuthorInfo
{
    public string Bio { get; init; } = "";
    public string? PhotoPath { get; init; }
    public string? PhotoCaption { get; init; }
    public List<SocialLink> SocialLinks { get; init; } = new();
}

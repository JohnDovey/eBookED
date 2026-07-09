namespace eBookEditor.Core.Models;

public record ProjectFile
{
    public int SchemaVersion { get; init; } = 1;
    public BookMetadata Metadata { get; set; } = new();
    public List<SpineItem> Spine { get; set; } = new();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

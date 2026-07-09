namespace eBookEditor.Core.Models;

public record ChapterFrontMatter
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public ChapterNumberMode NumberMode { get; init; } = ChapterNumberMode.Auto;
    public int? NumberOverride { get; init; }
}

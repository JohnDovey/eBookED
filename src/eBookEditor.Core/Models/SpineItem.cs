using System.Text.Json.Serialization;

namespace eBookEditor.Core.Models;

public enum SpineItemType
{
    FrontMatter,
    Chapter,
    BackMatter
}

public enum ChapterNumberMode
{
    Auto,
    Override,
    None
}

public record SpineItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public SpineItemType Type { get; init; }
    public string RelativePath { get; init; } = "";
    public int Order { get; set; }
    public bool IsGenerated { get; init; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public ChapterNumberMode NumberMode { get; set; } = ChapterNumberMode.Auto;
    public int? NumberOverride { get; set; }
    public int? ResolvedNumber { get; set; }

    /// <summary>"1. Chapter Title" for numbered chapters, otherwise just the title — used by
    /// the sidebar to render the spine as an ordered list without duplicating this logic in
    /// the view layer. Not persisted; always recomputed from the other properties.</summary>
    [JsonIgnore]
    public string DisplayTitle => Type == SpineItemType.Chapter && ResolvedNumber is { } number
        ? $"{number}. {Title}"
        : Title ?? RelativePath;
}

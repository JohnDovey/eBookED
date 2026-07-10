namespace eBookEditor.Core.Models;

public record ProjectFile
{
    // 1 = legacy Markdown-bodied chapters (.md). 2 = HTML-bodied chapters (.ebhtml), from the
    // HTML content-model refactor onward. New projects are created at the current version;
    // loading a version-1 project isn't rejected here yet — the actual "require migration
    // before editing" gate is built as part of the migration tool itself (see the refactor
    // plan's Phase 6), not this bump, which only marks the format going forward.
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public BookMetadata Metadata { get; set; } = new();
    public List<SpineItem> Spine { get; set; } = new();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

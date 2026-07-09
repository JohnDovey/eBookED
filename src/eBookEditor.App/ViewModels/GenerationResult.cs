namespace eBookEditor.App.ViewModels;

/// <summary>Outcome of an EPUB/PDF export, shown to the user in GenerationResultWindow.
/// PageCount is null for EPUB (reflowable, no fixed pages).</summary>
public record GenerationResult(bool Success, string FormatName, string? OutputPath, string? ErrorMessage, int? WordCount, int? PageCount);

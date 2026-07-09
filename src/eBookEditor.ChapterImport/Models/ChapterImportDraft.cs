using eBookEditor.DocxImport.Models;

namespace eBookEditor.ChapterImport.Models;

/// <summary>A chapter ready to be written to disk and inserted into the spine.
/// <paramref name="PositionHint"/> comes from a leading number in the source file's name
/// (e.g. "23. What Now.md"), or null when the file had no such hint.</summary>
public record ChapterImportDraft(string Title, string BodyMarkdown, int? PositionHint, IReadOnlyList<ExtractedImage> Images);

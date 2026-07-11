using eBookEditor.Core.Models;
using eBookEditor.DocxImport.Models;

namespace eBookEditor.ChapterImport.Models;

/// <summary>A chapter ready to be written to disk and inserted into the spine.
/// <paramref name="PositionHint"/> comes from a leading number in the source file's name
/// (e.g. "23. What Now.md"), or null when the file had no such hint. Type/NumberMode default
/// to a regular numbered chapter; SpecialPageClassifier sets them when the source's title
/// matches a recognized front-matter, back-matter, or unnumbered divider pattern.</summary>
public record ChapterImportDraft(
    string Title,
    string Body,
    int? PositionHint,
    IReadOnlyList<ExtractedImage> Images,
    SpineItemType Type = SpineItemType.Chapter,
    ChapterNumberMode NumberMode = ChapterNumberMode.Auto);

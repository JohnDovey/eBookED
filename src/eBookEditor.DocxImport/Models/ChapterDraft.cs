using eBookEditor.Core.Models;

namespace eBookEditor.DocxImport.Models;

public record ExtractedImage(string FileName, byte[] Bytes);

/// <summary>Type/NumberMode default to a regular numbered chapter; SpecialPageClassifier sets
/// them when a heading's text matches a recognized front-matter, back-matter, or unnumbered
/// mid-book divider ("Part One") pattern.</summary>
public record ChapterDraft(
    string Title,
    string Body,
    IReadOnlyList<ExtractedImage> Images,
    SpineItemType Type = SpineItemType.Chapter,
    ChapterNumberMode NumberMode = ChapterNumberMode.Auto);

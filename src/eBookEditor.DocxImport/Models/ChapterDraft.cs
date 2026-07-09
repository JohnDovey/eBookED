namespace eBookEditor.DocxImport.Models;

public record ExtractedImage(string FileName, byte[] Bytes);

public record ChapterDraft(string Title, string BodyMarkdown, IReadOnlyList<ExtractedImage> Images);

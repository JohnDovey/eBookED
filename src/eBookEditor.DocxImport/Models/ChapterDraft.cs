namespace eBookEditor.DocxImport.Models;

public record ExtractedImage(string FileName, byte[] Bytes);

public record ChapterDraft(string Title, string Body, IReadOnlyList<ExtractedImage> Images);

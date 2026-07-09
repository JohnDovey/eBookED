using System.Text;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Markdown.Services;

public class MarkdownExportService
{
    private readonly ChapterFileService _chapterFileService = new();

    public string ExportWholeBook(EbookProject project)
    {
        var sb = new StringBuilder();
        var ordered = project.Spine.OrderBy(i => i.Order).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            sb.Append(RenderSection(project, ordered[i]));

            if (i < ordered.Count - 1)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public string ExportChapter(EbookProject project, SpineItem item) => RenderSection(project, item);

    private string RenderSection(EbookProject project, SpineItem item)
    {
        var path = project.ResolvePath(item);
        var rawText = File.ReadAllText(path);
        var (_, body) = _chapterFileService.ParseChapter(rawText);

        // Generated front/back matter pages already carry their own headings; chapter
        // files don't (title/subtitle live only in YAML front matter), so synthesize one.
        if (item.Type != SpineItemType.Chapter)
            return body.TrimEnd() + "\n";

        var heading = item.ResolvedNumber is { } number
            ? $"# Chapter {number}: {item.Title}"
            : $"# {item.Title}";

        var sb = new StringBuilder();
        sb.AppendLine(heading);

        if (!string.IsNullOrWhiteSpace(item.Subtitle))
        {
            sb.AppendLine();
            sb.AppendLine($"## {item.Subtitle}");
        }

        sb.AppendLine();
        sb.AppendLine(body.TrimEnd());
        return sb.ToString();
    }
}

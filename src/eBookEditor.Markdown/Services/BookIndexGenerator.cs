using System.Text;
using eBookEditor.Core.Models;

namespace eBookEditor.Markdown.Services;

public class BookIndexGenerator
{
    public string GenerateBookMd(EbookProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {project.Metadata.Title}");
        sb.AppendLine();
        sb.AppendLine("_This file is auto-regenerated whenever the spine changes. Edit chapter order and content_");
        sb.AppendLine("_via the app rather than editing this file's links directly._");
        sb.AppendLine();

        AppendGroup(sb, "Front Matter", project.Spine, SpineItemType.FrontMatter);
        AppendGroup(sb, "Chapters", project.Spine, SpineItemType.Chapter);
        AppendGroup(sb, "Back Matter", project.Spine, SpineItemType.BackMatter);

        return sb.ToString();
    }

    private static void AppendGroup(StringBuilder sb, string heading, IReadOnlyList<SpineItem> spine, SpineItemType type)
    {
        var items = spine.Where(i => i.Type == type).OrderBy(i => i.Order).ToList();
        if (items.Count == 0)
            return;

        sb.AppendLine($"## {heading}");
        sb.AppendLine();

        foreach (var item in items)
        {
            var label = item is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {item.ResolvedNumber}: {item.Title}"
                : item.Title ?? item.RelativePath;

            sb.AppendLine($"- [{label}]({item.RelativePath})");
        }

        sb.AppendLine();
    }
}

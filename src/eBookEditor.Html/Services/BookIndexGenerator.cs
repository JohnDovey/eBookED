using System.Net;
using System.Text;
using eBookEditor.Core.Models;

namespace eBookEditor.Html.Services;

public class BookIndexGenerator
{
    public string GenerateBookMd(EbookProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<h1>{Encode(project.Metadata.Title)}</h1>");
        sb.AppendLine("<p><em>This file is auto-regenerated whenever the spine changes. Edit chapter order and content");
        sb.AppendLine("via the app rather than editing this file's links directly.</em></p>");

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

        sb.AppendLine($"<h2>{heading}</h2>");
        sb.AppendLine("<ul>");

        foreach (var item in items)
        {
            var label = item is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {item.ResolvedNumber}: {item.Title}"
                : item.Title ?? item.RelativePath;

            sb.AppendLine($"<li><a href=\"{Encode(item.RelativePath)}\">{Encode(label)}</a></li>");
        }

        sb.AppendLine("</ul>");
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}

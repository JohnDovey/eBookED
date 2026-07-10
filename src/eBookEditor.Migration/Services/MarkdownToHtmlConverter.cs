using Markdig;

namespace eBookEditor.Migration.Services;

/// <summary>Converts a legacy project's Markdown chapter bodies to HTML during the one-time
/// upgrade migration (see ProjectMigrator) — the only remaining use of Markdig in this app.</summary>
public class MarkdownToHtmlConverter
{
    private readonly MarkdownPipeline _pipeline = MarkdownPipelineFactory.Create();

    public string ToHtml(string markdown) => Markdig.Markdown.ToHtml(markdown, _pipeline);
}

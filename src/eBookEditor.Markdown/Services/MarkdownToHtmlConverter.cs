using Markdig;

namespace eBookEditor.Markdown.Services;

public class MarkdownToHtmlConverter
{
    private readonly MarkdownPipeline _pipeline = MarkdownPipelineFactory.Create();

    public string ToHtml(string markdown) => Markdig.Markdown.ToHtml(markdown, _pipeline);
}

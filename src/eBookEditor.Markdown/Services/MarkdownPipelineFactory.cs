using Markdig;

namespace eBookEditor.Markdown.Services;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create() => new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();
}

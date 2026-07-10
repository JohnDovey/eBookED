using Markdig;

namespace eBookEditor.Migration.Services;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create() => new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();
}

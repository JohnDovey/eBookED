using ReverseMarkdown;

namespace eBookEditor.ChapterImport.Services;

public class HtmlToMarkdownConverter
{
    private readonly Converter _converter = new(new Config
    {
        GithubFlavored = true,
        Tags = { Unknown = Config.UnknownTagsOption.PassThrough },
        Formatting = { RemoveComments = true },
        Links = { SmartHref = true }
    });

    public string Convert(string html) => _converter.Convert(html).Trim();
}

using eBookEditor.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace eBookEditor.Core.Services;

public class ChapterFileService
{
    private const string Delimiter = "---";

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public (ChapterFrontMatter FrontMatter, string Body) ReadChapter(string path)
        => ParseChapter(File.ReadAllText(path));

    public (ChapterFrontMatter FrontMatter, string Body) ParseChapter(string text)
    {
        if (!text.StartsWith(Delimiter, StringComparison.Ordinal))
            return (new ChapterFrontMatter(), text);

        var endIndex = text.IndexOf($"\n{Delimiter}", Delimiter.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return (new ChapterFrontMatter(), text);

        var yaml = text[Delimiter.Length..endIndex].Trim();
        var body = text[(endIndex + 1 + Delimiter.Length)..].TrimStart('\n', '\r');

        var frontMatter = string.IsNullOrWhiteSpace(yaml)
            ? new ChapterFrontMatter()
            : _deserializer.Deserialize<ChapterFrontMatter>(yaml) ?? new ChapterFrontMatter();

        return (frontMatter, body);
    }

    public void WriteChapter(string path, ChapterFrontMatter frontMatter, string body)
    {
        var yaml = _serializer.Serialize(frontMatter).TrimEnd('\n', '\r');
        var text = $"{Delimiter}\n{yaml}\n{Delimiter}\n\n{body.TrimStart('\n', '\r')}";
        File.WriteAllText(path, text);
    }

    public string CreateNewChapterFile(string chaptersDir, string title)
    {
        var slug = Slug.Create(title, "chapter");
        var fileName = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}.md";
        var path = Path.Combine(chaptersDir, fileName);
        WriteChapter(path, new ChapterFrontMatter { Title = title }, "");
        return path;
    }
}

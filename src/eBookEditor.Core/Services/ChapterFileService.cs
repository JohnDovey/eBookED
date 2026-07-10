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
        var fileName = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}.ebhtml";
        var path = Path.Combine(chaptersDir, fileName);
        WriteChapter(path, new ChapterFrontMatter { Title = title }, "");
        return path;
    }

    /// <summary>
    /// Renames chapter files on disk to match their resolved position ("023 - Chapter
    /// Name.ebhtml"), so they sort correctly in a file browser too. Renames happen in two
    /// passes — first to unique temp names, then to their final names — so that swapping
    /// two chapters' positions (which briefly makes their desired file names collide with
    /// each other) never fails or overwrites data. Mutates <paramref name="project"/>'s
    /// spine in place (SpineItem.RelativePath is replaced via `with` since it's init-only).
    /// </summary>
    public void SyncChapterFileNames(EbookProject project)
    {
        var chapters = project.Spine
            .Where(i => i.Type == SpineItemType.Chapter)
            .OrderBy(i => i.Order)
            .ToList();

        var pending = new List<(SpineItem Chapter, string DesiredFileName)>();
        foreach (var chapter in chapters)
        {
            var desiredFileName = ChapterFileNaming.BuildFileName(chapter.ResolvedNumber, chapter.Title ?? "Untitled");
            var currentPath = project.ResolvePath(chapter);
            if (Path.GetFileName(currentPath) == desiredFileName)
                continue;

            var tempPath = Path.Combine(project.ChaptersDir, $".{Guid.NewGuid():N}.tmp");
            File.Move(currentPath, tempPath);

            var tempChapter = chapter with { RelativePath = $"{ProjectPaths.ChaptersDirName}/{Path.GetFileName(tempPath)}" };
            ReplaceInSpine(project, tempChapter);
            pending.Add((tempChapter, desiredFileName));
        }

        foreach (var (chapter, desiredFileName) in pending)
        {
            var tempPath = project.ResolvePath(chapter);
            var finalPath = Path.Combine(project.ChaptersDir, desiredFileName);
            File.Move(tempPath, finalPath);

            var finalChapter = chapter with { RelativePath = $"{ProjectPaths.ChaptersDirName}/{desiredFileName}" };
            ReplaceInSpine(project, finalChapter);
        }
    }

    private static void ReplaceInSpine(EbookProject project, SpineItem item)
    {
        var index = project.Spine.FindIndex(i => i.Id == item.Id);
        project.Spine[index] = item;
    }
}

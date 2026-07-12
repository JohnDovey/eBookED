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

        // Defensive: a well-formed chapter file never has more than one front-matter block, but
        // a rare historical bug (confirmed against a real user project — a stray second block,
        // stacked directly on top of the real body, presumably from some past operation that
        // fed an already-front-mattered file's full text back in as if it were plain body) can
        // leave one behind. Left alone, that stale block renders as literal "---\ntitle: ..."
        // text at the top of the chapter in every exported format. Strip any further leading
        // "---...---" block(s) too, discarding their content — the first block (already parsed
        // above) stays authoritative, matching this project's own spine metadata — so the file
        // self-heals the moment it's read, and permanently the next time it's saved.
        while (body.StartsWith(Delimiter, StringComparison.Ordinal))
        {
            var staleEndIndex = body.IndexOf($"\n{Delimiter}", Delimiter.Length, StringComparison.Ordinal);
            if (staleEndIndex < 0)
                break;

            body = body[(staleEndIndex + 1 + Delimiter.Length)..].TrimStart('\n', '\r');
        }

        return (frontMatter, body);
    }

    /// <summary>
    /// Replaces just the body portion of a raw chapter/page file's text, leaving its front
    /// matter block byte-for-byte as it was — used to fold a WYSIWYG-edited body (which only
    /// ever sees/edits the body, never the front matter — see MainWindow.PushContentToWysiwyg)
    /// back into the editor's full CurrentText without re-serializing the YAML through
    /// ChapterFrontMatter, which could reformat it (key order, quoting) even when nothing the
    /// user actually touched changed.
    /// </summary>
    public string ReplaceBody(string text, string newBody)
    {
        if (!text.StartsWith(Delimiter, StringComparison.Ordinal))
            return newBody;

        var endIndex = text.IndexOf($"\n{Delimiter}", Delimiter.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return newBody;

        var prefix = text[..(endIndex + 1 + Delimiter.Length)];
        return $"{prefix}\n\n{newBody.TrimStart('\n', '\r')}";
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
    /// Renames chapter files on disk to match their resolved position ("023-Chapter-
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

using eBookEditor.Core.Models;

namespace eBookEditor.Core.Services;

public class SpineService
{
    public event Action<EbookProject>? SpineChanged;

    /// <summary>
    /// Adds a new chapter. When <paramref name="positionHint"/> is given (e.g. parsed from a
    /// dropped file's name like "23. What Now.md" via ChapterFileNaming.ParseHint), the
    /// chapter is inserted so that hint-1 chapters precede it, clamped to the current chapter
    /// count; otherwise it's appended after the last chapter, as before.
    /// </summary>
    public SpineItem AddChapter(EbookProject project, string title, string relativePath, int? positionHint = null)
    {
        var item = new SpineItem
        {
            Type = SpineItemType.Chapter,
            RelativePath = relativePath,
            Title = title,
            NumberMode = ChapterNumberMode.Auto
        };

        if (positionHint is { } hint)
        {
            var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
            var insertBeforeIndex = Math.Clamp(hint - 1, 0, chapters.Count);
            if (insertBeforeIndex >= chapters.Count)
            {
                project.Spine.Add(item);
            }
            else
            {
                var spineIndex = project.Spine.FindIndex(i => i.Id == chapters[insertBeforeIndex].Id);
                project.Spine.Insert(spineIndex, item);
            }
        }
        else
        {
            project.Spine.Add(item);
        }

        NormalizeOrder(project);
        RenumberChapters(project);
        RaiseSpineChanged(project);
        return item;
    }

    public void AddGeneratedItem(EbookProject project, SpineItem item)
    {
        project.Spine.Add(item);
        NormalizeOrder(project);
        RenumberChapters(project);
        RaiseSpineChanged(project);
    }

    public void RemoveItem(EbookProject project, Guid itemId)
    {
        project.Spine.RemoveAll(i => i.Id == itemId);
        NormalizeOrder(project);
        RenumberChapters(project);
        RaiseSpineChanged(project);
    }

    public void ReorderChapters(EbookProject project, IReadOnlyList<Guid> newChapterOrderIds)
    {
        var chapters = project.Spine.Where(i => i.Type == SpineItemType.Chapter).ToDictionary(i => i.Id);
        if (newChapterOrderIds.Count != chapters.Count || newChapterOrderIds.Any(id => !chapters.ContainsKey(id)))
            throw new ArgumentException("New chapter order must contain exactly the current set of chapter ids.", nameof(newChapterOrderIds));

        var frontMatter = project.Spine.Where(i => i.Type == SpineItemType.FrontMatter).ToList();
        var backMatter = project.Spine.Where(i => i.Type == SpineItemType.BackMatter).ToList();
        var reorderedChapters = newChapterOrderIds.Select(id => chapters[id]).ToList();

        project.Spine.Clear();
        project.Spine.AddRange(frontMatter);
        project.Spine.AddRange(reorderedChapters);
        project.Spine.AddRange(backMatter);

        NormalizeOrder(project);
        RenumberChapters(project);
        RaiseSpineChanged(project);
    }

    public void RenumberChapters(EbookProject project)
    {
        var chapters = project.Spine
            .Where(i => i.Type == SpineItemType.Chapter)
            .OrderBy(i => i.Order)
            .ToList();

        var next = 1;
        foreach (var chapter in chapters)
        {
            chapter.ResolvedNumber = chapter.NumberMode switch
            {
                ChapterNumberMode.None => null,
                ChapterNumberMode.Override => chapter.NumberOverride,
                _ => next
            };
            if (chapter.NumberMode == ChapterNumberMode.Auto)
                next++;
        }
    }

    public void NormalizeOrder(EbookProject project)
    {
        var ordered = project.Spine
            .OrderBy(GroupRank)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Order = i;
    }

    private static int GroupRank(SpineItem item) => item.Type switch
    {
        SpineItemType.FrontMatter => 0,
        SpineItemType.Chapter => 1,
        SpineItemType.BackMatter => 2,
        _ => 3
    };

    private void RaiseSpineChanged(EbookProject project) => SpineChanged?.Invoke(project);
}

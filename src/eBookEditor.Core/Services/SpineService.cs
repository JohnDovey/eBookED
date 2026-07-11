using eBookEditor.Core.Models;

namespace eBookEditor.Core.Services;

public enum SpineMoveDirection
{
    Up,
    Down
}

public class SpineService
{
    public event Action<EbookProject>? SpineChanged;

    /// <summary>
    /// Adds a new chapter. When <paramref name="positionHint"/> is given (e.g. parsed from a
    /// dropped file's name like "23. What Now.md" via ChapterFileNaming.ParseHint), the
    /// chapter is inserted so that hint-1 chapters precede it, clamped to the current chapter
    /// count; otherwise it's appended after the last chapter, as before.
    /// </summary>
    public SpineItem AddChapter(EbookProject project, string title, string relativePath, int? positionHint = null) =>
        InsertChapterLikeItem(project, title, relativePath, ChapterNumberMode.Auto, positionHint);

    /// <summary>
    /// Adds an unnumbered mid-book divider ("Part One", "Part Two") — a chapter-like item
    /// with NumberMode.None, so RenumberChapters skips it without shifting the numbers of
    /// chapters before or after it, and every TOC/heading renderer that already special-cases
    /// a null ResolvedNumber shows just its title, with no "Chapter N:" prefix.
    /// </summary>
    public SpineItem AddChapterDivider(EbookProject project, string title, string relativePath, int? positionHint = null) =>
        InsertChapterLikeItem(project, title, relativePath, ChapterNumberMode.None, positionHint);

    private SpineItem InsertChapterLikeItem(EbookProject project, string title, string relativePath, ChapterNumberMode numberMode, int? positionHint)
    {
        var item = new SpineItem
        {
            Type = SpineItemType.Chapter,
            RelativePath = relativePath,
            Title = title,
            NumberMode = numberMode
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

    /// <summary>Adds a custom, optional front-matter page (Acknowledgements, Preface,
    /// Dedication, etc.) to the end of the front-matter group — already numbered with
    /// lowercase roman numerals in PDF export, since that count is just "how many FrontMatter
    /// items sit at the front of the spine," not tied to the 4 fixed generated pages.</summary>
    public SpineItem AddFrontMatterItem(EbookProject project, string title, string relativePath) =>
        AddMatterItem(project, SpineItemType.FrontMatter, title, relativePath);

    /// <summary>Adds a custom, optional back-matter page (Afterword, Postscript, Index, Also
    /// By the Author, etc.) to the end of the back-matter group.</summary>
    public SpineItem AddBackMatterItem(EbookProject project, string title, string relativePath) =>
        AddMatterItem(project, SpineItemType.BackMatter, title, relativePath);

    private SpineItem AddMatterItem(EbookProject project, SpineItemType type, string title, string relativePath)
    {
        var item = new SpineItem
        {
            Type = type,
            RelativePath = relativePath,
            Title = title
        };

        project.Spine.Add(item);
        NormalizeOrder(project);
        RenumberChapters(project);
        RaiseSpineChanged(project);
        return item;
    }

    /// <summary>Swaps a front-matter or back-matter item's position with its neighbor, one
    /// step at a time. A no-op at the top/bottom of the spine or when the neighbor belongs to
    /// a different group (Order is always contiguous within a group — see NormalizeOrder —
    /// so a different-type neighbor means the group edge was reached).</summary>
    public void MoveItem(EbookProject project, Guid itemId, SpineMoveDirection direction)
    {
        var ordered = project.Spine.OrderBy(i => i.Order).ToList();
        var index = ordered.FindIndex(i => i.Id == itemId);
        if (index < 0)
            return;

        var swapIndex = direction == SpineMoveDirection.Up ? index - 1 : index + 1;
        if (swapIndex < 0 || swapIndex >= ordered.Count)
            return;

        var item = ordered[index];
        var neighbor = ordered[swapIndex];
        if (neighbor.Type != item.Type)
            return;

        (item.Order, neighbor.Order) = (neighbor.Order, item.Order);
        RenumberChapters(project);
        RaiseSpineChanged(project);
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

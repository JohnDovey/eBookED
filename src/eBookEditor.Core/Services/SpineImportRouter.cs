using eBookEditor.Core.Models;

namespace eBookEditor.Core.Services;

/// <summary>
/// Shared dispatch logic for routing an imported item to the right SpineService Add* method
/// and directory, based on its classified type/number mode (see SpecialPageClassifier) — a
/// regular numbered chapter, an unnumbered mid-book divider, or a custom front/back-matter
/// page. Used by both MainWindowViewModel (importing into an already-open project) and
/// EpubProjectImporter (importing while a project is still being created), so the four-way
/// switch on SpineItemType/ChapterNumberMode lives in exactly one place.
/// </summary>
public class SpineImportRouter
{
    private readonly SpineService _spineService;

    public SpineImportRouter(SpineService spineService)
    {
        _spineService = spineService;
    }

    /// <summary>The directory a newly-imported item's file should be written to, based on its
    /// classified type — chapters and unnumbered dividers alike live in chapters/, since a
    /// divider is still a Chapter-type spine item.</summary>
    public static string ChapterDirFor(EbookProject project, SpineItemType type) => type switch
    {
        SpineItemType.FrontMatter => project.FrontMatterDir,
        SpineItemType.BackMatter => project.BackMatterDir,
        _ => project.ChaptersDir
    };

    public SpineItem AddImportedItemToSpine(EbookProject project, string title, string relativePath, SpineItemType type, ChapterNumberMode numberMode, int? positionHint = null)
    {
        var effectiveTitle = DisambiguateIfCollidesWithGeneratedPage(project, title);
        return type switch
        {
            SpineItemType.FrontMatter => _spineService.AddFrontMatterItem(project, effectiveTitle, relativePath),
            SpineItemType.BackMatter => _spineService.AddBackMatterItem(project, effectiveTitle, relativePath),
            _ when numberMode == ChapterNumberMode.None => _spineService.AddChapterDivider(project, effectiveTitle, relativePath, positionHint),
            _ => _spineService.AddChapter(project, effectiveTitle, relativePath, positionHint)
        };
    }

    /// <summary>SpecialPageClassifier recognizes "About the Author"/"Index" headings as
    /// back-matter, but every project already has its own generated page at those exact
    /// titles (About the Author is seeded at project creation; Index is auto-seeded the first
    /// time "Generate/Regenerate Index" runs) — importing a document with its own such heading
    /// would otherwise create a second, genuinely duplicate-looking entry (confusingly empty
    /// too, since the generated one's real content comes from metadata/marked entries, not from
    /// this imported file). Renaming the imported one instead preserves its content as its own
    /// distinct page rather than silently discarding it.</summary>
    private static readonly Dictionary<string, string> GeneratedPageFileNamesByTitle = new(StringComparer.OrdinalIgnoreCase)
    {
        ["About the Author"] = ProjectPaths.AboutAuthorPageFileName,
        ["Index"] = ProjectPaths.IndexPageFileName,
    };

    private static string DisambiguateIfCollidesWithGeneratedPage(EbookProject project, string title)
    {
        if (!GeneratedPageFileNamesByTitle.TryGetValue(title.Trim(), out var generatedFileName))
            return title;

        var alreadyExists = project.Spine.Any(i => i.RelativePath.EndsWith(generatedFileName, StringComparison.Ordinal));
        return alreadyExists ? $"{title} (Imported)" : title;
    }
}

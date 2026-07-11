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

    public SpineItem AddImportedItemToSpine(EbookProject project, string title, string relativePath, SpineItemType type, ChapterNumberMode numberMode, int? positionHint = null) => type switch
    {
        SpineItemType.FrontMatter => _spineService.AddFrontMatterItem(project, title, relativePath),
        SpineItemType.BackMatter => _spineService.AddBackMatterItem(project, title, relativePath),
        _ when numberMode == ChapterNumberMode.None => _spineService.AddChapterDivider(project, title, relativePath, positionHint),
        _ => _spineService.AddChapter(project, title, relativePath, positionHint)
    };
}

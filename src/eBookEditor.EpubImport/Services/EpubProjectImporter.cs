using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Html.Services;

using FragmentKey = (string TargetHref, string Fragment);

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Creates a brand-new eBookEditor project on disk from a source EPUB — the "Create Project
/// from ePub" command's actual work, driven by EpubImportService's parsed result. Follows the
/// same create-project-then-populate-spine shape MainWindowViewModel.ImportDocx/
/// ImportChapterFiles use for importing into an already-open project, just running before any
/// project (or MainWindowViewModel) exists yet — see SpineImportRouter, the shared dispatch
/// logic both this class and MainWindowViewModel call.
///
/// Runs in five stages: (1) create the project and populate its spine from every parsed
/// chapter draft, (2) sync chapter file names to their resolved position — every downstream
/// stage below depends on file paths being final, (3) build a new CSS template from the
/// source's own stylesheet(s), if any, via EpubStylesheetImporter, (4) rewrite internal
/// cross-chapter hyperlinks now that every item's final path is known, via
/// EpubInternalHrefRewriter, (5) regenerate the generated pages and save.
/// </summary>
public class EpubProjectImporter
{
    private readonly EpubImportService _epubImportService = new();
    private readonly ProjectService _projectService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly SpineService _spineService = new();
    private readonly SpineImportRouter _spineImportRouter;
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();
    private readonly TemplateService _templateService;
    private readonly FontService _fontService;

    public EpubProjectImporter(TemplateService? templateService = null, FontService? fontService = null)
    {
        _spineImportRouter = new SpineImportRouter(_spineService);
        _templateService = templateService ?? new TemplateService();
        _fontService = fontService ?? new FontService();
    }

    /// <summary>Parses the EPUB fresh (cheap relative to project creation) so a caller can
    /// pre-fill a "Project Name" field from the real dc:title before the user confirms
    /// anything — used by EpubImportWizardWindow's initial load.</summary>
    public string SuggestProjectName(string epubPath)
    {
        var metadata = _epubImportService.Import(epubPath).Metadata;
        return string.IsNullOrWhiteSpace(metadata.Title)
            ? Path.GetFileNameWithoutExtension(epubPath)
            : metadata.Title;
    }

    public EbookProject Import(string epubPath, string destinationDir, string projectName)
    {
        var result = _epubImportService.Import(epubPath);
        var metadata = result.Metadata with { Title = projectName };

        var project = _projectService.CreateProject(destinationDir, projectName, metadata);

        var itemIds = new Guid?[result.Items.Count];
        for (var i = 0; i < result.Items.Count; i++)
        {
            var draft = result.Items[i];
            var path = _chapterFileService.CreateNewChapterFile(SpineImportRouter.ChapterDirFor(project, draft.Type), draft.Title);
            _chapterFileService.WriteChapter(path, new ChapterFrontMatter { Title = draft.Title }, draft.Body);
            var relativePath = Path.GetRelativePath(project.DirectoryPath, path).Replace('\\', '/');
            var item = _spineImportRouter.AddImportedItemToSpine(project, draft.Title, relativePath, draft.Type, draft.NumberMode);
            itemIds[i] = item.Id;

            foreach (var image in draft.Images)
                File.WriteAllBytes(Path.Combine(project.ImagesDir, image.FileName), image.Bytes);
        }

        if (result.CoverImageBytes is { } coverBytes && result.CoverImageFileName is { Length: > 0 } coverFileName)
        {
            File.WriteAllBytes(Path.Combine(project.ImagesDir, coverFileName), coverBytes);
            project.ProjectFile.Metadata = project.Metadata with { CoverImagePath = $"{ProjectPaths.ImagesDirName}/{coverFileName}" };
        }

        _chapterFileService.SyncChapterFileNames(project);

        if (result.SourceStylesheets.Count > 0)
        {
            var templateName = BuildTemplateFromSource(project, result);
            project.ProjectFile.Metadata = project.Metadata with { SelectedTemplate = templateName };
        }

        RewriteInternalLinks(project, result, itemIds);

        _pageGenerator.RegenerateAllGeneratedPages(project);
        File.WriteAllText(project.BookMdPath, _bookIndexGenerator.GenerateBookMd(project));
        _projectService.SaveProject(project);

        return project;
    }

    /// <summary>Merges the source EPUB's own stylesheet(s) against Vellum Serif.css (see
    /// EpubStylesheetImporter) and writes the result as a new template — named after the book,
    /// collision-suffixed against whatever templates already exist — plus any of its own fonts
    /// the merge kept, into TemplateService's real runtime templates/fonts directories (not the
    /// source tree — those are shared, app-wide locations an end user's import writes into at
    /// runtime, unlike a developer-authored template shipped in the repo). Returns the new
    /// template's name.</summary>
    private string BuildTemplateFromSource(EbookProject project, EpubImportResult result)
    {
        var vellumCss = _templateService.GetTemplateCss("Vellum Serif");
        var (mergedCss, fontsToWrite) = EpubStylesheetImporter.BuildTemplate(result.SourceStylesheets, result.SourceFontFilesByFileName, vellumCss);

        var templateName = UniqueTemplateName(project.Metadata.Title);
        Directory.CreateDirectory(_templateService.TemplatesDirectory);
        File.WriteAllText(Path.Combine(_templateService.TemplatesDirectory, $"{templateName}.css"), mergedCss);

        Directory.CreateDirectory(_fontService.FontsDirectory);
        foreach (var (fileName, bytes) in fontsToWrite)
        {
            var fontPath = Path.Combine(_fontService.FontsDirectory, fileName);
            if (!File.Exists(fontPath))
                File.WriteAllBytes(fontPath, bytes);
        }

        return templateName;
    }

    private string UniqueTemplateName(string bookTitle)
    {
        var sanitized = SanitizeFileNameComponent(string.IsNullOrWhiteSpace(bookTitle) ? "Imported" : bookTitle);
        var existingNames = new HashSet<string>(_templateService.ScanTemplateNames(), StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(sanitized))
            return sanitized;

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{sanitized} {index}";
            index++;
        } while (existingNames.Contains(candidate));

        return candidate;
    }

    private static readonly char[] InvalidFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static string SanitizeFileNameComponent(string name)
    {
        var sanitized = new string(name.Select(c => InvalidFileNameChars.Contains(c) || char.IsControl(c) ? '-' : c).ToArray()).Trim();
        return sanitized.Length == 0 ? "Imported" : sanitized;
    }

    /// <summary>
    /// Rewrites internal cross-chapter hrefs now that SyncChapterFileNames has assigned every
    /// item's final RelativePath. A same/cross-chapter link into a specific in-page section
    /// (e.g. "chapter3.xhtml#section2") is converted into a real, working jump using this app's
    /// own "dest:" cross-document link convention (see InternalLinkConvention) when the
    /// fragment's target element can actually be found in that chapter's body; otherwise it
    /// falls back to the previous chapter-level-only rewrite (see EpubInternalHrefRewriter's
    /// class doc comment) — a documented "best effort" simplification, not a silently dropped
    /// feature.
    /// </summary>
    private void RewriteInternalLinks(EbookProject project, EpubImportResult result, Guid?[] itemIds)
    {
        var sourceHrefToRelativePath = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceHrefToItem = new Dictionary<string, SpineItem>(StringComparer.Ordinal);
        for (var i = 0; i < result.Items.Count; i++)
        {
            var sourceHref = result.SourceHrefsByItem[i];
            if (sourceHref is null || itemIds[i] is not { } itemId)
                continue;

            var currentItem = project.Spine.FirstOrDefault(s => s.Id == itemId);
            if (currentItem is not null)
            {
                sourceHrefToRelativePath[sourceHref] = currentItem.RelativePath;
                sourceHrefToItem[sourceHref] = currentItem;
            }
        }

        if (sourceHrefToRelativePath.Count == 0)
            return;

        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sourceHref, item) in sourceHrefToItem)
            bodies[sourceHref] = _chapterFileService.ReadChapter(project.ResolvePath(item)).Body;

        var fragmentDestIds = BuildFragmentDestinations(bodies, sourceHrefToRelativePath);

        var idRenamesByTargetHref = fragmentDestIds
            .GroupBy(entry => entry.Key.TargetHref)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g.ToDictionary(e => e.Key.Fragment, e => e.Value));

        foreach (var (sourceHref, item) in sourceHrefToItem)
        {
            var path = project.ResolvePath(item);
            var (frontMatter, body) = _chapterFileService.ReadChapter(path);

            var withRenamedIds = idRenamesByTargetHref.TryGetValue(sourceHref, out var idRenames)
                ? EpubInternalHrefRewriter.RenameIds(body, idRenames)
                : body;
            var rewritten = EpubInternalHrefRewriter.RewriteHrefsWithFragments(
                withRenamedIds, sourceHref, sourceHrefToRelativePath, fragmentDestIds);

            if (rewritten != body)
                _chapterFileService.WriteChapter(path, frontMatter, rewritten);
        }
    }

    /// <summary>Scans every chapter for anchor-fragment references (same-chapter "#id" or
    /// cross-chapter "other.xhtml#id"), keeping only the ones whose target element is actually
    /// found in the referenced chapter's body (a fragment pointing nowhere real isn't converted
    /// into a dest: link to nowhere), and assigns each a document-wide-unique "dest:" id derived
    /// from the original fragment — collisions are possible since two different source chapters
    /// can each legitimately use the same id value (e.g. both have an "#intro"), which this
    /// app's own dest: ids must not (PDF Section names/Word bookmark names are document-wide).</summary>
    private static IReadOnlyDictionary<FragmentKey, string> BuildFragmentDestinations(
        IReadOnlyDictionary<string, string> bodies, IReadOnlyDictionary<string, string> sourceHrefToRelativePath)
    {
        var result = new Dictionary<FragmentKey, string>();
        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (sourceHref, body) in bodies)
        {
            foreach (var (targetHref, fragment) in EpubInternalHrefRewriter.FindFragmentReferences(body))
            {
                // EpubFootnoteConverter has already run by this point (see EpubImportService),
                // converting footnote references into this app's own "fn:"/"fnref:" convention
                // — those are same-page anchor-fragment links too, but must stay exactly as
                // EpubFootnoteConverter/MainWindow.OnInsertFootnoteClick expect, not get folded
                // into a generic "dest:" link destination.
                if (fragment.StartsWith("fn:", StringComparison.Ordinal) || fragment.StartsWith("fnref:", StringComparison.Ordinal))
                    continue;

                var effectiveTargetHref = targetHref.Length == 0 ? sourceHref : targetHref;
                var key = (effectiveTargetHref, fragment);
                if (result.ContainsKey(key) || !sourceHrefToRelativePath.ContainsKey(effectiveTargetHref))
                    continue;

                if (!bodies.TryGetValue(effectiveTargetHref, out var targetBody) || !EpubInternalHrefRewriter.HasId(targetBody, fragment))
                    continue;

                var baseSlug = Slug.Create(fragment, "target");
                var slug = baseSlug;
                var suffix = 2;
                while (!usedSlugs.Add(slug))
                    slug = $"{baseSlug}-{suffix++}";

                result[key] = $"{InternalLinkConvention.DestinationIdPrefix}{slug}";
            }
        }

        return result;
    }
}

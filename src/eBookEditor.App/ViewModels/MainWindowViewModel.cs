using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eBookEditor.ChapterImport.Models;
using eBookEditor.ChapterImport.Services;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.DocxImport.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Html.Services;
using eBookEditor.Migration.Services;
using eBookEditor.Pdf.Services;

namespace eBookEditor.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly SpineImportRouter _spineImportRouter;
    private readonly ChapterFileService _chapterFileService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();
    private readonly IndexEntryScanner _indexEntryScanner = new();
    private readonly HtmlBookAssembler _htmlBookAssembler = new();
    private readonly DocxImportService _docxImportService = new();
    private readonly HtmlToDocxConverter _htmlToDocxConverter = new();
    private readonly ChapterImportService _chapterImportService = new();
    private readonly OrphanChapterScanner _orphanScanner = new();
    private readonly ProjectMigrator _projectMigrator = new();
    private readonly TemplateService _templateService;
    private readonly EpubBuilder _epubBuilder;
    private readonly PdfBuilder _pdfBuilder;
    private readonly AppSettingsService _appSettingsService;
    private readonly FontInstallerService _fontInstallerService;

    public EditorViewModel Editor { get; } = new();
    public MetadataViewModel Metadata { get; } = new();

    [ObservableProperty]
    private EbookProject _currentProject;

    [ObservableProperty]
    private SpineItem? _selectedSpineItem;

    [ObservableProperty]
    private string _chapterTitleInput = string.Empty;

    [ObservableProperty]
    private string _chapterSubtitleInput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private GenerationResult? _lastExportResult;

    public bool IsChapterSelected => SelectedSpineItem?.Type == SpineItemType.Chapter;

    /// <summary>Gates the title/subheading rename form: any chapter (including unnumbered
    /// dividers), or a custom front/back-matter page — but not the fixed generated pages
    /// (title/copyright/TOC/about-author), whose titles come from metadata, not free text.</summary>
    public bool IsRenamableItemSelected => SelectedSpineItem is { } item && IsRenamable(item);

    private static bool IsRenamable(SpineItem item) =>
        item.Type == SpineItemType.Chapter || (item.Type is SpineItemType.FrontMatter or SpineItemType.BackMatter && !item.IsGenerated);

    public IReadOnlyList<SpineItem> SpineItems => CurrentProject.Spine.OrderBy(i => i.Order).ToList();

    public MainWindowViewModel(
        EbookProject project,
        AppSettingsService? appSettingsService = null,
        TemplateService? templateService = null,
        FontInstallerService? fontInstallerService = null)
    {
        _currentProject = project;
        _appSettingsService = appSettingsService ?? new AppSettingsService(new AppPaths());
        _spineImportRouter = new SpineImportRouter(_spineService);
        _templateService = templateService ?? new TemplateService();
        _epubBuilder = new EpubBuilder(_templateService);
        _pdfBuilder = new PdfBuilder(_templateService);
        _fontInstallerService = fontInstallerService ?? new FontInstallerService();
        Metadata.LoadFrom(project.Metadata);

        if (FindTitlePageItem(project) is { } titlePage)
            OpenSpineItem(titlePage);

        ImportOrphanedChapterFiles();
        _appSettingsService.RecordProjectOpened(project.DirectoryPath);
    }

    private static SpineItem? FindTitlePageItem(EbookProject project) => project.Spine
        .FirstOrDefault(i => i.RelativePath.EndsWith(ProjectPaths.TitlePageFileName, StringComparison.Ordinal));

    /// <summary>
    /// Renames chapter files on disk to match their resolved position (see
    /// ChapterFileService.SyncChapterFileNames), then refreshes SelectedSpineItem/Editor's
    /// notion of the current file if the selected chapter was one of the ones renamed —
    /// SpineItem.RelativePath is replaced via `with`, so any previously-held reference goes
    /// stale the moment a rename happens.
    /// </summary>
    private void SyncChapterFileNamesAndRefreshSelection()
    {
        _chapterFileService.SyncChapterFileNames(CurrentProject);

        if (SelectedSpineItem is not { } selected)
            return;

        var refreshed = CurrentProject.Spine.FirstOrDefault(i => i.Id == selected.Id);
        if (refreshed is null || ReferenceEquals(refreshed, selected))
            return;

        SelectedSpineItem = refreshed;
        Editor.FilePath = CurrentProject.ResolvePath(refreshed);
    }

    /// <summary>
    /// Picks up chapter-shaped files sitting in chapters/ that aren't referenced by any spine
    /// item — e.g. dropped into the folder directly via Finder/Explorer — and adds them to the
    /// book. Filename hints (see ChapterFileNaming.ParseHint) determine where they land. An
    /// orphan already in this app's native .ebhtml format is registered in place, same as
    /// always. Any other orphan (.md, .docx, .html, .htm) is silently converted: its content is
    /// imported the same way a drag-and-dropped file would be (see ImportChapterFiles), written
    /// out as a brand new .ebhtml chapter file, and — only once that conversion has actually
    /// succeeded and the new file is safely added to the spine — the original non-native file
    /// is deleted, so the project doesn't end up with two copies of the same chapter in two
    /// formats.
    /// </summary>
    private void ImportOrphanedChapterFiles()
    {
        var orphanPaths = _orphanScanner.FindOrphanedChapterFiles(CurrentProject);
        if (orphanPaths.Count == 0)
            return;

        foreach (var filePath in orphanPaths)
        {
            var isNative = string.Equals(Path.GetExtension(filePath), ".ebhtml", StringComparison.OrdinalIgnoreCase);
            var drafts = _chapterImportService.ImportFile(filePath);

            foreach (var draft in drafts)
            {
                if (isNative)
                {
                    var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, filePath).Replace('\\', '/');
                    _spineImportRouter.AddImportedItemToSpine(CurrentProject, draft.Title, relativePath, draft.Type, draft.NumberMode, draft.PositionHint);
                    continue;
                }

                var newPath = _chapterFileService.CreateNewChapterFile(SpineImportRouter.ChapterDirFor(CurrentProject, draft.Type), draft.Title);
                _chapterFileService.WriteChapter(newPath, new ChapterFrontMatter { Title = draft.Title }, draft.Body);
                var newRelativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, newPath).Replace('\\', '/');
                _spineImportRouter.AddImportedItemToSpine(CurrentProject, draft.Title, newRelativePath, draft.Type, draft.NumberMode, draft.PositionHint);

                foreach (var image in draft.Images)
                    File.WriteAllBytes(Path.Combine(CurrentProject.ImagesDir, image.FileName), image.Bytes);
            }

            if (!isNative)
                File.Delete(filePath);
        }

        SyncChapterFileNamesAndRefreshSelection();
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        StatusMessage = $"Found and added {orphanPaths.Count} chapter file(s) not previously in the book.";
    }

    /// <summary>Repositions a custom front/back-matter page one step within its own group —
    /// see SpineService.MoveItem.</summary>
    public void MoveItem(SpineItem item, SpineMoveDirection direction)
    {
        _spineService.MoveItem(CurrentProject, item.Id, direction);
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
    }

    public IReadOnlyList<string> GetRecentProjectPaths() => _appSettingsService.Load().RecentProjectPaths;

    /// <summary>Called when this window closes, so the next app launch doesn't try to restore it.</summary>
    public void RecordProjectClosed() => _appSettingsService.RecordProjectClosed(CurrentProject.DirectoryPath);

    partial void OnSelectedSpineItemChanged(SpineItem? value)
    {
        OnPropertyChanged(nameof(IsChapterSelected));
        OnPropertyChanged(nameof(IsRenamableItemSelected));
        ChapterTitleInput = value?.Title ?? string.Empty;
        ChapterSubtitleInput = value?.Subtitle ?? string.Empty;
    }

    public void RefreshAvailableTemplates() => Metadata.RefreshAvailableTemplates(_templateService);

    /// <summary>The current project's selected CSS template, for the WYSIWYG editor pane and
    /// Preview window (both render via HtmlPageShell, real CSS, real browser engine).</summary>
    public string GetCurrentTemplateCss() => _templateService.GetTemplateCss(CurrentProject.Metadata.SelectedTemplate);

    /// <summary>An arbitrary named template's CSS — unlike GetCurrentTemplateCss, not tied to
    /// the project's saved SelectedTemplate, so the Style window's "Preview" button can render
    /// whatever the template picker currently has selected, even if it hasn't been saved yet.</summary>
    public string GetTemplateCss(string? templateName) => _templateService.GetTemplateCss(templateName);

    /// <summary>
    /// Installs any fonts the given template's stylesheet requires onto the host system if
    /// they aren't there yet. Called whenever the template picker's selection changes.
    /// </summary>
    public void EnsureTemplateFontsInstalled(string? templateName)
    {
        var css = _templateService.GetTemplateCss(templateName);
        var installed = _fontInstallerService.EnsureFontsInstalled(css);
        if (installed.Count > 0)
            StatusMessage = $"Installed font(s): {string.Join(", ", installed)}";
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (Editor.IsDirty)
            Editor.Save();

        CurrentProject.ProjectFile.Metadata = WithPreservedIdentifier(Metadata.ToMetadata());
        _projectService.SaveProject(CurrentProject);
        StatusMessage = $"Saved project to {CurrentProject.DirectoryPath}";
    }

    /// <summary>
    /// MetadataViewModel.ToMetadata() always builds a brand-new BookMetadata from the
    /// ViewModel's form fields — Identifier isn't one of them (it's an internal book-identity
    /// GUID, never user-editable), so BookMetadata.Identifier's own "= Guid.NewGuid()" default
    /// kicked in on every single metadata save, silently replacing a project's real identifier
    /// with a fresh random one (confirmed via the EPUB's own dc:identifier, built from this
    /// field in EpubBuilder.ResolveUniqueIdentifier, changing on every export even with zero
    /// content changes). Both save paths below must re-apply the identifier already on disk
    /// before overwriting CurrentProject.ProjectFile.Metadata, so it only ever changes when
    /// CreateProject mints a real new one for a brand-new project.
    /// </summary>
    private BookMetadata WithPreservedIdentifier(BookMetadata metadata) =>
        metadata with { Identifier = CurrentProject.ProjectFile.Metadata.Identifier };

    public void SaveMetadataAndRegenerate()
    {
        var metadata = WithPreservedIdentifier(Metadata.ToMetadata());
        CurrentProject.ProjectFile.Metadata = metadata;
        _projectService.SaveProject(CurrentProject);

        foreach (var contributor in metadata.Contributors)
            _appSettingsService.RecordContributorUsed(contributor.Name, contributor.Role);
        if (metadata.Publisher is { } publisher)
            _appSettingsService.RecordPublisherUsed(publisher);

        RegenerateGeneratedContent();
    }

    /// <summary>
    /// Pre-fills currently-empty metadata fields with the most recently used values from
    /// other projects. Never overwrites a field the user has already filled in.
    /// </summary>
    public void ApplyAutofillDefaultsIfEmpty()
    {
        var settings = _appSettingsService.Load();

        if (Metadata.Authors.Count == 0 && settings.KnownAuthorNames.Count > 0)
            Metadata.Authors.Add(ContributorEntry.FromFullName(settings.KnownAuthorNames[0]));

        if (Metadata.Editors.Count == 0 && settings.KnownEditorNames.Count > 0)
            Metadata.Editors.Add(ContributorEntry.FromFullName(settings.KnownEditorNames[0]));

        if (Metadata.Illustrators.Count == 0 && settings.KnownIllustratorNames.Count > 0)
            Metadata.Illustrators.Add(ContributorEntry.FromFullName(settings.KnownIllustratorNames[0]));

        if (string.IsNullOrWhiteSpace(Metadata.PublisherName) && settings.KnownPublishers.Count > 0)
        {
            Metadata.PublisherName = settings.KnownPublishers[0].Name;
            Metadata.PublisherLogoPath = settings.KnownPublishers[0].LogoPath ?? string.Empty;
        }
    }

    public void OpenSpineItem(SpineItem item)
    {
        if (Editor.IsDirty)
            Editor.Save();

        SelectedSpineItem = item;
        var path = CurrentProject.ResolvePath(item);
        if (File.Exists(path))
            Editor.LoadFile(path);
    }

    [RelayCommand]
    private void AddChapter()
    {
        const string title = "New Chapter";
        var path = _chapterFileService.CreateNewChapterFile(CurrentProject.ChaptersDir, title);
        var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');

        var item = _spineService.AddChapter(CurrentProject, title, relativePath);
        _chapterFileService.SyncChapterFileNames(CurrentProject);
        item = CurrentProject.Spine.First(i => i.Id == item.Id);

        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        OpenSpineItem(item);
    }

    /// <summary>Adds an unnumbered mid-book divider ("Part One", "Part Two") — see
    /// SpineService.AddChapterDivider.</summary>
    [RelayCommand]
    private void AddPartBreak()
    {
        const string title = "New Part";
        var path = _chapterFileService.CreateNewChapterFile(CurrentProject.ChaptersDir, title);
        var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');

        var item = _spineService.AddChapterDivider(CurrentProject, title, relativePath, PositionHintAfterSelectedChapter());
        _chapterFileService.SyncChapterFileNames(CurrentProject);
        item = CurrentProject.Spine.First(i => i.Id == item.Id);

        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        OpenSpineItem(item);
    }

    /// <summary>A 1-based SpineService position hint placing a new chapter-like item
    /// immediately after the currently selected chapter (or divider — both are Type.Chapter),
    /// or null to append at the end when nothing/a non-chapter item is selected.</summary>
    private int? PositionHintAfterSelectedChapter()
    {
        if (SelectedSpineItem is not { Type: SpineItemType.Chapter } selected)
            return null;

        var chapters = CurrentProject.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        var index = chapters.FindIndex(c => c.Id == selected.Id);
        return index < 0 ? null : index + 2;
    }

    /// <summary>Adds a custom, optional front-matter page (Acknowledgements, Preface,
    /// Dedication, etc.) — see SpineService.AddFrontMatterItem.</summary>
    [RelayCommand]
    private void AddFrontMatterPage()
    {
        const string title = "New Front Matter Page";
        var path = _chapterFileService.CreateNewChapterFile(CurrentProject.FrontMatterDir, title);
        var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');

        var item = _spineService.AddFrontMatterItem(CurrentProject, title, relativePath);

        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        OpenSpineItem(item);
    }

    /// <summary>Adds a custom, optional back-matter page (Afterword, Postscript, Index, Also
    /// By the Author, etc.) — see SpineService.AddBackMatterItem.</summary>
    [RelayCommand]
    private void AddBackMatterPage()
    {
        const string title = "New Back Matter Page";
        var path = _chapterFileService.CreateNewChapterFile(CurrentProject.BackMatterDir, title);
        var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');

        var item = _spineService.AddBackMatterItem(CurrentProject, title, relativePath);

        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        OpenSpineItem(item);
    }

    /// <summary>
    /// Imports one or more dropped/picked files as new chapters: .ebhtml/.md are used as-is,
    /// .docx reuses the whole-manuscript importer's chapter-boundary detection, and .html/.htm
    /// are sanitized (see HtmlImportSanitizer). Each file's name supplies both a title and an
    /// optional spine position hint (e.g. "23. What Now.md" -> chapter 23) — see
    /// ChapterFileNaming.ParseHint. Files with an unsupported extension are silently skipped,
    /// so dropping a folder's worth of mixed chapter/image files doesn't need pre-filtering by
    /// the caller.
    /// </summary>
    public void ImportChapterFiles(IReadOnlyList<string> filePaths)
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var importedCount = 0;
            foreach (var filePath in filePaths)
            {
                IReadOnlyList<ChapterImportDraft> drafts;
                try
                {
                    drafts = _chapterImportService.ImportFile(filePath);
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                foreach (var draft in drafts)
                {
                    var path = _chapterFileService.CreateNewChapterFile(SpineImportRouter.ChapterDirFor(CurrentProject, draft.Type), draft.Title);
                    _chapterFileService.WriteChapter(path, new ChapterFrontMatter { Title = draft.Title }, draft.Body);
                    var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');
                    _spineImportRouter.AddImportedItemToSpine(CurrentProject, draft.Title, relativePath, draft.Type, draft.NumberMode, draft.PositionHint);

                    foreach (var image in draft.Images)
                        File.WriteAllBytes(Path.Combine(CurrentProject.ImagesDir, image.FileName), image.Bytes);

                    importedCount++;
                }
            }

            if (importedCount == 0)
            {
                StatusMessage = "No supported chapter files (.md, .docx, .html) were found to import.";
                return;
            }

            SyncChapterFileNamesAndRefreshSelection();
            _projectService.SaveProject(CurrentProject);
            RegenerateGeneratedContent();
            StatusMessage = $"Imported {importedCount} chapter(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chapter import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveChapterHeader()
    {
        if (SelectedSpineItem is not { } item || !IsRenamable(item))
            return;

        var path = CurrentProject.ResolvePath(item);
        var (frontMatter, body) = _chapterFileService.ReadChapter(path);
        var updatedFrontMatter = frontMatter with { Title = ChapterTitleInput, Subtitle = ChapterSubtitleInput };
        _chapterFileService.WriteChapter(path, updatedFrontMatter, body);

        item.Title = ChapterTitleInput;
        item.Subtitle = ChapterSubtitleInput;
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
    }

    public void ReorderChapters(IReadOnlyList<Guid> newChapterOrderIds)
    {
        _spineService.ReorderChapters(CurrentProject, newChapterOrderIds);
        SyncChapterFileNamesAndRefreshSelection();
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
    }

    /// <summary>Deletes a chapter's file and removes it from the spine. If it was the open
    /// file, the editor is pointed at the title page instead so it isn't left referencing a
    /// deleted file.</summary>
    public void DeleteChapter(SpineItem item)
    {
        if (item.Type != SpineItemType.Chapter)
            return;

        var wasSelected = SelectedSpineItem?.Id == item.Id;
        var path = CurrentProject.ResolvePath(item);
        if (File.Exists(path))
            File.Delete(path);

        _spineService.RemoveItem(CurrentProject, item.Id);
        SyncChapterFileNamesAndRefreshSelection();
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();

        if (wasSelected && FindTitlePageItem(CurrentProject) is { } titlePage)
            OpenSpineItem(titlePage);

        StatusMessage = $"Deleted chapter \"{item.Title}\".";
    }

    /// <summary>Exports a single chapter's HTML body as a .docx file — the reverse of
    /// Import DOCX/Import Chapters.</summary>
    public void ExportChapterAsWord(SpineItem item)
    {
        if (item.Type != SpineItemType.Chapter)
            return;

        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var (_, body) = _chapterFileService.ReadChapter(CurrentProject.ResolvePath(item));
            if (ChapterHeadingHtml.Build(item, body) is { } heading)
                body = heading + "\n" + body;

            var fileName = Slug.Create(item.Title ?? "chapter", "chapter") + ".docx";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            var sourceDir = Path.GetDirectoryName(CurrentProject.ResolvePath(item));
            var templateCss = _templateService.GetTemplateCss(CurrentProject.Metadata.SelectedTemplate);
            _htmlToDocxConverter.ConvertToFile(body, item.Title ?? "Untitled Chapter", outputPath, sourceDir, templateCss);
            StatusMessage = $"Exported chapter to {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Word export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RegenerateFrontMatter() => RegenerateGeneratedContent();

    /// <summary>
    /// Scans every chapter/page for "Mark as Index Entry" markers (see IndexEntryScanner) and
    /// (re)writes the back-matter "Index" page from them — auto-seeding that page into the
    /// spine on first use, the same way ProjectService.CreateProject seeds "About the Author"
    /// (see SpineService.AddBackMatterItem). An explicit command, not a side effect of every
    /// edit — scanning every chapter's body on every save would be too expensive for that; see
    /// PageGeneratorService.GenerateIndexPage for the actual page-content logic.
    /// </summary>
    [RelayCommand]
    private void GenerateIndex()
    {
        if (Editor.IsDirty)
            Editor.Save();

        var occurrences = _indexEntryScanner.FindAll(CurrentProject);

        var indexItem = CurrentProject.Spine.FirstOrDefault(i => i.RelativePath.EndsWith(ProjectPaths.IndexPageFileName, StringComparison.Ordinal));
        if (indexItem is null)
        {
            var path = Path.Combine(CurrentProject.BackMatterDir, ProjectPaths.IndexPageFileName);
            File.WriteAllText(path, string.Empty);
            indexItem = _spineService.AddBackMatterItem(CurrentProject, "Index", $"{ProjectPaths.BackMatterDirName}/{ProjectPaths.IndexPageFileName}");
        }

        File.WriteAllText(CurrentProject.ResolvePath(indexItem), _pageGenerator.GenerateIndexPage(occurrences));

        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();

        var termCount = occurrences.Select(o => o.Term).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        StatusMessage = $"Index regenerated: {termCount} term(s), {occurrences.Count} occurrence(s).";
    }

    /// <summary>Whether CurrentProject still has any chapter/front/back matter file in the
    /// legacy Markdown ".md" format — i.e. was created before the HTML content-model
    /// refactor and hasn't been upgraded yet. Checked fresh each time rather than cached,
    /// since it only needs to be read right before showing the "Upgrade Project to HTML…"
    /// confirmation dialog.</summary>
    public bool ProjectNeedsHtmlMigration => _projectMigrator.NeedsMigration(CurrentProject);

    /// <summary>
    /// Upgrades CurrentProject from the legacy Markdown format to this app's native HTML
    /// format: backs up the whole project directory first, then converts every chapter/front/
    /// back matter file's body from Markdown to HTML in place (see ProjectMigrator — hand-
    /// edited generated pages are converted verbatim, not regenerated from metadata, so
    /// hand-edits survive). A no-op, safely, if the project doesn't need it.
    /// </summary>
    public void UpgradeProjectToHtml()
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var backupPath = _projectMigrator.CreateBackup(CurrentProject);
            var result = _projectMigrator.MigrateToHtml(CurrentProject);

            OnPropertyChanged(nameof(SpineItems));
            if (FindTitlePageItem(CurrentProject) is { } titlePage)
                OpenSpineItem(titlePage);

            StatusMessage = $"Upgraded {result.ConvertedFileCount} file(s) to HTML. Backup saved to {backupPath}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Upgrade to HTML failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportEpub()
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var fileName = Slug.Create(CurrentProject.Metadata.Title, "book") + ".epub";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            _epubBuilder.Build(CurrentProject, outputPath);
            StatusMessage = $"Exported EPUB to {outputPath}";

            var wordCount = CurrentProject.Spine
                .Where(i => i.Type == SpineItemType.Chapter)
                .Sum(i => HtmlText.CountWords(_chapterFileService.ReadChapter(CurrentProject.ResolvePath(i)).Body));
            LastExportResult = new GenerationResult(true, "EPUB", outputPath, null, wordCount, null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"EPUB export failed: {ex.Message}";
            LastExportResult = new GenerationResult(false, "EPUB", null, ex.Message, null, null);
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var fileName = Slug.Create(CurrentProject.Metadata.Title, "book") + ".pdf";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            var result = _pdfBuilder.Build(CurrentProject, outputPath);
            StatusMessage = $"Exported PDF to {outputPath}";
            LastExportResult = new GenerationResult(true, "PDF", outputPath, null, result.WordCount, result.PageCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF export failed: {ex.Message}";
            LastExportResult = new GenerationResult(false, "PDF", null, ex.Message, null, null);
        }
    }

    [RelayCommand]
    private void ExportWordWholeBook()
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var html = _htmlBookAssembler.AssembleWholeBook(CurrentProject);
            var fileName = Slug.Create(CurrentProject.Metadata.Title, "book") + ".docx";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            var templateCss = _templateService.GetTemplateCss(CurrentProject.Metadata.SelectedTemplate);
            // Front matter/chapters/back matter each reference images as "../images/…"
            // relative to their own directory, but those directories are all siblings of
            // images/ at the project root, so any one of them resolves correctly for every
            // section in the whole-book HTML — ChaptersDir is as good as any.
            _htmlToDocxConverter.ConvertToFile(html, CurrentProject.Metadata.Title, outputPath, CurrentProject.ChaptersDir, templateCss);
            StatusMessage = $"Exported whole book Word document to {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Word export failed: {ex.Message}";
        }
    }

    public void ImportDocx(string docxPath)
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var chapterDrafts = _docxImportService.Import(docxPath);
            if (chapterDrafts.Count == 0)
            {
                StatusMessage = "No chapters were detected in the selected document.";
                return;
            }

            foreach (var draft in chapterDrafts)
            {
                var path = _chapterFileService.CreateNewChapterFile(SpineImportRouter.ChapterDirFor(CurrentProject, draft.Type), draft.Title);
                _chapterFileService.WriteChapter(path, new ChapterFrontMatter { Title = draft.Title }, draft.Body);
                var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');
                _spineImportRouter.AddImportedItemToSpine(CurrentProject, draft.Title, relativePath, draft.Type, draft.NumberMode);

                foreach (var image in draft.Images)
                    File.WriteAllBytes(Path.Combine(CurrentProject.ImagesDir, image.FileName), image.Bytes);
            }

            _projectService.SaveProject(CurrentProject);
            RegenerateGeneratedContent();
            StatusMessage = $"Imported {chapterDrafts.Count} chapter(s) from {Path.GetFileName(docxPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"DOCX import failed: {ex.Message}";
        }
    }

    private void RegenerateGeneratedContent()
    {
        if (Editor.IsDirty)
            Editor.Save();

        _pageGenerator.RegenerateAllGeneratedPages(CurrentProject);
        File.WriteAllText(CurrentProject.BookMdPath, _bookIndexGenerator.GenerateBookMd(CurrentProject));
        OnPropertyChanged(nameof(SpineItems));

        if (Editor.FilePath is { } path && File.Exists(path))
            Editor.LoadFile(path);
    }
}

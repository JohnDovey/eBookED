using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.DocxImport.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly ChapterFileService _chapterFileService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();
    private readonly MarkdownExportService _markdownExportService = new();
    private readonly DocxImportService _docxImportService = new();
    private readonly TemplateService _templateService;
    private readonly EpubBuilder _epubBuilder;
    private readonly AppSettingsService _appSettingsService;

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

    public bool IsChapterSelected => SelectedSpineItem?.Type == SpineItemType.Chapter;

    public IReadOnlyList<SpineItem> SpineItems => CurrentProject.Spine.OrderBy(i => i.Order).ToList();

    public MainWindowViewModel(EbookProject project, AppSettingsService? appSettingsService = null, TemplateService? templateService = null)
    {
        _currentProject = project;
        _appSettingsService = appSettingsService ?? new AppSettingsService(new AppPaths());
        _templateService = templateService ?? new TemplateService();
        _epubBuilder = new EpubBuilder(_templateService);
        Metadata.LoadFrom(project.Metadata);

        if (FindTitlePageItem(project) is { } titlePage)
            OpenSpineItem(titlePage);

        _appSettingsService.RecordProjectOpened(project.DirectoryPath);
    }

    private static SpineItem? FindTitlePageItem(EbookProject project) => project.Spine
        .FirstOrDefault(i => i.RelativePath.EndsWith(ProjectPaths.TitlePageFileName, StringComparison.Ordinal));

    partial void OnSelectedSpineItemChanged(SpineItem? value)
    {
        OnPropertyChanged(nameof(IsChapterSelected));
        ChapterTitleInput = value?.Title ?? string.Empty;
        ChapterSubtitleInput = value?.Subtitle ?? string.Empty;
    }

    public void RefreshAvailableTemplates() => Metadata.RefreshAvailableTemplates(_templateService);

    [RelayCommand]
    private void SaveProject()
    {
        if (Editor.IsDirty)
            Editor.Save();

        CurrentProject.ProjectFile.Metadata = Metadata.ToMetadata();
        _projectService.SaveProject(CurrentProject);
        StatusMessage = $"Saved project to {CurrentProject.DirectoryPath}";
    }

    public void SaveMetadataAndRegenerate()
    {
        var metadata = Metadata.ToMetadata();
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
            Metadata.Authors.Add(new ContributorEntry { Name = settings.KnownAuthorNames[0] });

        if (Metadata.Editors.Count == 0 && settings.KnownEditorNames.Count > 0)
            Metadata.Editors.Add(new ContributorEntry { Name = settings.KnownEditorNames[0] });

        if (Metadata.Illustrators.Count == 0 && settings.KnownIllustratorNames.Count > 0)
            Metadata.Illustrators.Add(new ContributorEntry { Name = settings.KnownIllustratorNames[0] });

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
            Editor.LoadFile(path, forcePreviewMode: item.IsGenerated);
    }

    [RelayCommand]
    private void AddChapter()
    {
        const string title = "New Chapter";
        var path = _chapterFileService.CreateNewChapterFile(CurrentProject.ChaptersDir, title);
        var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');

        var item = _spineService.AddChapter(CurrentProject, title, relativePath);
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
        OpenSpineItem(item);
    }

    [RelayCommand]
    private void SaveChapterHeader()
    {
        if (SelectedSpineItem is not { Type: SpineItemType.Chapter } item)
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
        _projectService.SaveProject(CurrentProject);
        RegenerateGeneratedContent();
    }

    [RelayCommand]
    private void RegenerateFrontMatter() => RegenerateGeneratedContent();

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"EPUB export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportMarkdownWholeBook()
    {
        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var markdown = _markdownExportService.ExportWholeBook(CurrentProject);
            var fileName = Slug.Create(CurrentProject.Metadata.Title, "book") + "-full.md";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            File.WriteAllText(outputPath, markdown);
            StatusMessage = $"Exported whole book markdown to {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Markdown export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportMarkdownChapter()
    {
        if (SelectedSpineItem is not { Type: SpineItemType.Chapter } item)
        {
            StatusMessage = "Select a chapter to export.";
            return;
        }

        try
        {
            if (Editor.IsDirty)
                Editor.Save();

            var markdown = _markdownExportService.ExportChapter(CurrentProject, item);
            var fileName = Slug.Create(item.Title ?? "chapter", "chapter") + ".md";
            var outputPath = Path.Combine(CurrentProject.OutputDir, fileName);
            File.WriteAllText(outputPath, markdown);
            StatusMessage = $"Exported chapter markdown to {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Markdown export failed: {ex.Message}";
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
                var path = _chapterFileService.CreateNewChapterFile(CurrentProject.ChaptersDir, draft.Title);
                _chapterFileService.WriteChapter(path, new ChapterFrontMatter { Title = draft.Title }, draft.BodyMarkdown);
                var relativePath = Path.GetRelativePath(CurrentProject.DirectoryPath, path).Replace('\\', '/');
                _spineService.AddChapter(CurrentProject, draft.Title, relativePath);

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
            Editor.LoadFile(path, forcePreviewMode: SelectedSpineItem?.IsGenerated ?? false);
    }
}

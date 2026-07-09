using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.App;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly AppSettingsService _appSettingsService;
    private readonly TemplateService _templateService;

    public MainWindowViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _appSettingsService = new AppSettingsService(new TestAppPaths(Path.Combine(_tempDir, "app-data")));
        _templateService = new TemplateService(Path.Combine(_tempDir, "templates"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private MainWindowViewModel NewViewModel(string projectName = "VM Test Book")
    {
        var metadata = new BookMetadata { Title = projectName, CopyrightHolder = "Test Author" };
        var project = _projectService.CreateProject(_tempDir, projectName, metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        return new MainWindowViewModel(project, _appSettingsService, _templateService);
    }

    [Fact]
    public void Constructor_LoadsTitlePageIntoEditor()
    {
        var vm = NewViewModel();

        Assert.Contains("VM Test Book", vm.Editor.CurrentText);
        Assert.Equal("VM Test Book", vm.Metadata.Title);
    }

    [Fact]
    public void AddChapter_AddsToSpineAndOpensInEditor()
    {
        var vm = NewViewModel();

        vm.AddChapterCommand.Execute(null);

        var chapter = Assert.Single(vm.SpineItems, i => i.Type == SpineItemType.Chapter);
        Assert.Equal("New Chapter", chapter.Title);
        Assert.Equal(vm.CurrentProject.ResolvePath(chapter), vm.Editor.FilePath);

        var tocText = File.ReadAllText(Path.Combine(vm.CurrentProject.FrontMatterDir, ProjectPaths.TocPageFileName));
        Assert.Contains("Chapter 1: New Chapter", tocText);
    }

    [Fact]
    public void SaveChapterHeader_UpdatesSpineItemFileAndToc()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);

        vm.ChapterTitleInput = "The Arrival";
        vm.ChapterSubtitleInput = "A new beginning";
        vm.SaveChapterHeaderCommand.Execute(null);

        var chapter = Assert.Single(vm.SpineItems, i => i.Type == SpineItemType.Chapter);
        Assert.Equal("The Arrival", chapter.Title);
        Assert.Equal("A new beginning", chapter.Subtitle);

        var (frontMatter, _) = new ChapterFileService().ReadChapter(vm.CurrentProject.ResolvePath(chapter));
        Assert.Equal("The Arrival", frontMatter.Title);

        var tocText = File.ReadAllText(Path.Combine(vm.CurrentProject.FrontMatterDir, ProjectPaths.TocPageFileName));
        Assert.Contains("Chapter 1: The Arrival", tocText);
    }

    [Fact]
    public void ReorderChapters_UpdatesSpineOrderAndToc()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        vm.ChapterTitleInput = "One";
        vm.SaveChapterHeaderCommand.Execute(null);

        vm.AddChapterCommand.Execute(null);
        vm.ChapterTitleInput = "Two";
        vm.SaveChapterHeaderCommand.Execute(null);

        var chapters = vm.SpineItems.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        var one = chapters.Single(c => c.Title == "One");
        var two = chapters.Single(c => c.Title == "Two");

        vm.ReorderChapters([two.Id, one.Id]);

        var reordered = vm.SpineItems.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["Two", "One"], reordered.Select(c => c.Title));
        Assert.Equal([1, 2], reordered.Select(c => c.ResolvedNumber));

        var tocText = File.ReadAllText(Path.Combine(vm.CurrentProject.FrontMatterDir, ProjectPaths.TocPageFileName));
        Assert.True(tocText.IndexOf("Chapter 1: Two", StringComparison.Ordinal) < tocText.IndexOf("Chapter 2: One", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenSpineItem_SavesDirtyEditorContentBeforeSwitching()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.SpineItems, i => i.Type == SpineItemType.Chapter);

        vm.Editor.CurrentText = "Unsaved chapter body.";
        Assert.True(vm.Editor.IsDirty);

        var copyrightItem = vm.SpineItems.Single(i => i.RelativePath.EndsWith(ProjectPaths.CopyrightPageFileName, StringComparison.Ordinal));
        vm.OpenSpineItem(copyrightItem);

        var (_, body) = new ChapterFileService().ReadChapter(vm.CurrentProject.ResolvePath(chapter));
        Assert.Contains("Unsaved chapter body.", body);
    }

    [Fact]
    public void SaveMetadataAndRegenerate_UpdatesProjectAndTitlePage()
    {
        var vm = NewViewModel();

        vm.Metadata.Title = "Renamed Book";
        vm.SaveMetadataAndRegenerate();

        Assert.Equal("Renamed Book", vm.CurrentProject.Metadata.Title);
        var titleText = File.ReadAllText(Path.Combine(vm.CurrentProject.FrontMatterDir, ProjectPaths.TitlePageFileName));
        Assert.Contains("Renamed Book", titleText);
    }

    [Fact]
    public void ExportEpub_WritesEpubFileToOutputDirAndReportsStatus()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);

        vm.ExportEpubCommand.Execute(null);

        var expectedPath = Path.Combine(vm.CurrentProject.OutputDir, "vm-test-book.epub");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains(expectedPath, vm.StatusMessage);
    }

    [Fact]
    public void ExportMarkdownWholeBook_WritesConcatenatedMarkdownFile()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);

        vm.ExportMarkdownWholeBookCommand.Execute(null);

        var expectedPath = Path.Combine(vm.CurrentProject.OutputDir, "vm-test-book-full.md");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains("VM Test Book", File.ReadAllText(expectedPath));
        Assert.Contains(expectedPath, vm.StatusMessage);
    }

    [Fact]
    public void ExportMarkdownChapter_WithChapterSelected_WritesChapterMarkdownFile()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        vm.ChapterTitleInput = "The Arrival";
        vm.SaveChapterHeaderCommand.Execute(null);

        vm.ExportMarkdownChapterCommand.Execute(null);

        var expectedPath = Path.Combine(vm.CurrentProject.OutputDir, "the-arrival.md");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains("# Chapter 1: The Arrival", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void ExportMarkdownChapter_WithNoChapterSelected_ReportsStatusWithoutWritingFile()
    {
        var vm = NewViewModel();

        vm.ExportMarkdownChapterCommand.Execute(null);

        Assert.Equal("Select a chapter to export.", vm.StatusMessage);
        Assert.False(Directory.Exists(vm.CurrentProject.OutputDir) && Directory.GetFiles(vm.CurrentProject.OutputDir).Length > 0);
    }

    [Fact]
    public void ImportDocx_AddsDetectedChaptersToSpineAndRegeneratesToc()
    {
        var vm = NewViewModel();
        var docxPath = eBookEditor.Tests.DocxImport.DocxFixtureBuilder.BuildSimpleDocx(
            Path.Combine(_tempDir, "import.docx"));

        vm.ImportDocx(docxPath);

        var chapters = vm.SpineItems.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(3, chapters.Count);
        Assert.Equal(["Chapter One", "Chapter Two", "Chapter 3: The Finale"], chapters.Select(c => c.Title));

        var tocText = File.ReadAllText(Path.Combine(vm.CurrentProject.FrontMatterDir, ProjectPaths.TocPageFileName));
        Assert.Contains("Chapter 1: Chapter One", tocText);
        Assert.Contains(Path.GetFileName(docxPath), vm.StatusMessage);
    }

    [Fact]
    public void SaveMetadataAndRegenerate_RecordsContributorsAndPublisherInAppSettings()
    {
        var vm = NewViewModel();
        vm.Metadata.AuthorNames = "Jane Doe";
        vm.Metadata.EditorNames = "Ed Itor";
        vm.Metadata.PublisherName = "Acme Press";

        vm.SaveMetadataAndRegenerate();

        var settings = _appSettingsService.Load();
        Assert.Contains("Jane Doe", settings.KnownAuthorNames);
        Assert.Contains("Ed Itor", settings.KnownEditorNames);
        Assert.Contains(settings.KnownPublishers, p => p.Name == "Acme Press");
    }

    [Fact]
    public void ApplyAutofillDefaultsIfEmpty_FillsOnlyEmptyFieldsFromKnownAppSettings()
    {
        var firstProject = NewViewModel("First Book");
        firstProject.Metadata.AuthorNames = "Jane Doe";
        firstProject.Metadata.PublisherName = "Acme Press";
        firstProject.SaveMetadataAndRegenerate();

        var secondProject = NewViewModel("Second Book");
        secondProject.Metadata.EditorNames = "Already Set Editor";

        secondProject.ApplyAutofillDefaultsIfEmpty();

        Assert.Equal("Jane Doe", secondProject.Metadata.AuthorNames);
        Assert.Equal("Acme Press", secondProject.Metadata.PublisherName);
        Assert.Equal("Already Set Editor", secondProject.Metadata.EditorNames);
    }

    [Fact]
    public void Constructor_RecordsProjectInRecentProjectsList()
    {
        var vm = NewViewModel();

        var settings = _appSettingsService.Load();

        Assert.Contains(vm.CurrentProject.DirectoryPath, settings.RecentProjectPaths);
    }

    [Fact]
    public void RefreshAvailableTemplates_SeedsDefaultAndPopulatesMetadataPickerList()
    {
        var vm = NewViewModel();

        vm.RefreshAvailableTemplates();

        Assert.Contains(TemplateService.DefaultTemplateName, vm.Metadata.AvailableTemplates);
        Assert.Equal(TemplateService.DefaultTemplateName, vm.Metadata.SelectedTemplate);
    }

    [Fact]
    public void SaveMetadataAndRegenerate_PersistsSelectedTemplateAndEpubExportUsesIt()
    {
        var vm = NewViewModel();
        vm.RefreshAvailableTemplates();
        Directory.CreateDirectory(_templateService.TemplatesDirectory);
        File.WriteAllText(Path.Combine(_templateService.TemplatesDirectory, "Elegant.css"), "body { color: navy; }");
        vm.RefreshAvailableTemplates();
        vm.Metadata.SelectedTemplate = "Elegant";

        vm.SaveMetadataAndRegenerate();
        Assert.Equal("Elegant", vm.CurrentProject.Metadata.SelectedTemplate);

        vm.ExportEpubCommand.Execute(null);
        var epubPath = Path.Combine(vm.CurrentProject.OutputDir, "vm-test-book.epub");
        using var archive = System.IO.Compression.ZipFile.OpenRead(epubPath);
        using var reader = new StreamReader(archive.GetEntry("OEBPS/styles.css")!.Open());
        Assert.Equal("body { color: navy; }", reader.ReadToEnd());
    }

    [Fact]
    public void SwitchToProject_SavesDirtyEditsAndLoadsNewProjectState()
    {
        var vm = NewViewModel("First Book");
        vm.Editor.CurrentText = "Unsaved title page edits.";
        var firstProjectPath = vm.CurrentProject.DirectoryPath;

        var secondMetadata = new BookMetadata { Title = "Second Book", CopyrightHolder = "Someone Else" };
        var secondProject = _projectService.CreateProject(_tempDir, "Second Book", secondMetadata);
        _pageGenerator.RegenerateAllGeneratedPages(secondProject);

        vm.SwitchToProject(secondProject);

        Assert.Equal("Second Book", vm.CurrentProject.Metadata.Title);
        Assert.Equal("Second Book", vm.Metadata.Title);
        Assert.False(vm.Editor.IsDirty);
        Assert.Contains("Second Book", vm.Editor.CurrentText);

        var firstTitlePage = File.ReadAllText(Path.Combine(firstProjectPath, "frontmatter", ProjectPaths.TitlePageFileName));
        Assert.Contains("Unsaved title page edits.", firstTitlePage);

        var settings = _appSettingsService.Load();
        Assert.Contains(secondProject.DirectoryPath, settings.RecentProjectPaths);
    }
}

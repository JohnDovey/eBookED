using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.App;

public class MainWindowViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly PageGeneratorService _pageGenerator = new();
    private readonly ChapterFileService _chapterFileService = new();
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
    public void OpenSpineItem_ChapterLoadsItsRawMarkdownIntoTheEditor()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.SpineItems, i => i.Type == SpineItemType.Chapter);

        vm.OpenSpineItem(chapter);

        Assert.Equal(chapter.Id, vm.SelectedSpineItem!.Id);
        Assert.Equal(vm.CurrentProject.ResolvePath(chapter), vm.Editor.FilePath);
    }

    [Fact]
    public void OpenSpineItem_GeneratedPageAlsoLoadsItsRawMarkdownIntoTheEditor()
    {
        // Generated pages (title/copyright/TOC/about-author) no longer force a separate
        // rendered-preview pane — the editor always shows raw Markdown for whatever's
        // selected, and rendering (for anything, generated or not) happens on demand in the
        // standalone preview window instead (see MainWindow.OnOpenPreviewClick).
        var vm = NewViewModel();

        var copyrightItem = vm.SpineItems.Single(i => i.RelativePath.EndsWith(ProjectPaths.CopyrightPageFileName, StringComparison.Ordinal));
        vm.OpenSpineItem(copyrightItem);

        Assert.Equal(vm.CurrentProject.ResolvePath(copyrightItem), vm.Editor.FilePath);
        Assert.Contains("Copyright", vm.Editor.CurrentText);
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
    public void ExportWordWholeBook_WritesDocxWithEveryChapter()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);

        vm.ExportWordWholeBookCommand.Execute(null);

        var expectedPath = Path.Combine(vm.CurrentProject.OutputDir, "vm-test-book.docx");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains(expectedPath, vm.StatusMessage);

        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(expectedPath, false);
        var bodyText = document.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("VM Test Book", bodyText);
        Assert.Contains("New Chapter", bodyText);
    }

    [Fact]
    public void ExportWordWholeBook_ImprintPageIncludesACreatedWithLineBeforeTheCopyrightStatement()
    {
        var vm = NewViewModel();

        vm.ExportWordWholeBookCommand.Execute(null);

        var expectedPath = Path.Combine(vm.CurrentProject.OutputDir, "vm-test-book.docx");
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(expectedPath, false);
        var bodyText = document.MainDocumentPart!.Document!.Body!.InnerText;

        var creditIndex = bodyText.IndexOf("Created with eBook Editor", StringComparison.Ordinal);
        var copyrightIndex = bodyText.IndexOf("Copyright ©", StringComparison.Ordinal);
        Assert.True(creditIndex >= 0, "expected a \"Created with eBook Editor\" credit line on the imprint page");
        Assert.True(creditIndex < copyrightIndex, "the credit line must appear before the copyright statement");
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
        vm.Metadata.Authors.Add(new ContributorEntry { FirstName = "Jane", LastName = "Doe" });
        vm.Metadata.Editors.Add(new ContributorEntry { FirstName = "Ed", LastName = "Itor" });
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
        firstProject.Metadata.Authors.Add(new ContributorEntry { FirstName = "Jane", LastName = "Doe" });
        firstProject.Metadata.PublisherName = "Acme Press";
        firstProject.SaveMetadataAndRegenerate();

        var secondProject = NewViewModel("Second Book");
        secondProject.Metadata.Editors.Add(new ContributorEntry { FirstName = "Already Set", LastName = "Editor" });

        secondProject.ApplyAutofillDefaultsIfEmpty();

        Assert.Equal("Jane", secondProject.Metadata.Authors.Single().FirstName);
        Assert.Equal("Doe", secondProject.Metadata.Authors.Single().LastName);
        Assert.Equal("Acme Press", secondProject.Metadata.PublisherName);
        Assert.Equal("Already Set", secondProject.Metadata.Editors.Single().FirstName);
        Assert.Equal("Editor", secondProject.Metadata.Editors.Single().LastName);
    }

    [Fact]
    public void Constructor_RecordsProjectInRecentProjectsList()
    {
        var vm = NewViewModel();

        var settings = _appSettingsService.Load();

        Assert.Contains(vm.CurrentProject.DirectoryPath, settings.RecentProjectPaths);
    }

    [Fact]
    public void GetRecentProjectPaths_ReturnsPathsFromAppSettings()
    {
        var vm = NewViewModel();

        var recent = vm.GetRecentProjectPaths();

        Assert.Contains(vm.CurrentProject.DirectoryPath, recent);
    }

    [Fact]
    public void RecordProjectClosed_RemovesProjectFromOpenProjectPaths()
    {
        var vm = NewViewModel();
        Assert.Contains(vm.CurrentProject.DirectoryPath, _appSettingsService.Load().OpenProjectPaths);

        vm.RecordProjectClosed();

        Assert.DoesNotContain(vm.CurrentProject.DirectoryPath, _appSettingsService.Load().OpenProjectPaths);
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
    public void SaveProjectCommand_PersistsMetadataAndFlushesDirtyEditorBuffer()
    {
        var vm = NewViewModel("First Book");
        vm.Editor.CurrentText = "Unsaved title page edits.";
        vm.Metadata.Title = "Renamed Book";

        vm.SaveProjectCommand.Execute(null);

        Assert.False(vm.Editor.IsDirty);
        Assert.Equal("Renamed Book", vm.CurrentProject.Metadata.Title);

        var reloaded = _projectService.LoadProject(vm.CurrentProject.DirectoryPath);
        Assert.Equal("Renamed Book", reloaded.Metadata.Title);

        var titlePage = File.ReadAllText(vm.Editor.FilePath!);
        Assert.Contains("Unsaved title page edits.", titlePage);
    }

    [Fact]
    public void ImportChapterFiles_AddsChapterAtItsFileNamesHintedPosition()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        vm.AddChapterCommand.Execute(null);

        var importPath = Path.Combine(_tempDir, "1. Imported First.md");
        File.WriteAllText(importPath, "Imported body text.");

        vm.ImportChapterFiles([importPath]);

        var chapters = vm.CurrentProject.Spine
            .Where(i => i.Type == SpineItemType.Chapter)
            .OrderBy(i => i.Order)
            .ToList();

        Assert.Equal(3, chapters.Count);
        Assert.Equal("Imported First", chapters[0].Title);
        // The imported file is still ".md" (import conversion is unrelated to this app's own
        // on-disk storage extension), but what gets written internally always uses this
        // project's own chapter-file convention — ".ebhtml" as of the HTML content-model
        // refactor.
        Assert.Equal("chapters/001-Imported-First.ebhtml", chapters[0].RelativePath);
        Assert.Contains("Imported body text.", File.ReadAllText(vm.CurrentProject.ResolvePath(chapters[0])));
    }

    [Fact]
    public void ImportChapterFiles_SkipsFilesWithUnsupportedExtensions()
    {
        var vm = NewViewModel();
        var imagePath = Path.Combine(_tempDir, "cover.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8, 0xFF, 0xD9]);

        vm.ImportChapterFiles([imagePath]);

        Assert.DoesNotContain(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);
        Assert.Contains("No supported chapter files", vm.StatusMessage);
    }

    [Fact]
    public void Constructor_PicksUpOrphanedChapterFilesInChaptersDirectoryAtTheirHintedPosition()
    {
        var metadata = new BookMetadata { Title = "Orphan Scan Book" };
        var project = _projectService.CreateProject(_tempDir, "Orphan Scan Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);

        // A tracked chapter already in the spine, plus an untracked ("orphan") file dropped
        // directly into chapters/ via Finder/Explorer, named to hint it belongs before it.
        File.WriteAllText(Path.Combine(project.ChaptersDir, "existing.ebhtml"), "Existing content.");
        new SpineService().AddChapter(project, "Existing Chapter", "chapters/existing.ebhtml");
        _projectService.SaveProject(project);

        File.WriteAllText(Path.Combine(project.ChaptersDir, "1. Found Chapter.ebhtml"), "Found content.");

        var vm = new MainWindowViewModel(project, _appSettingsService, _templateService);

        var chapters = vm.CurrentProject.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["Found Chapter", "Existing Chapter"], chapters.Select(c => c.Title));
    }

    [Fact]
    public void Constructor_ConvertsNonNativeOrphanFilesToEbhtmlAndDeletesTheOriginal()
    {
        var metadata = new BookMetadata { Title = "Orphan Conversion Book" };
        var project = _projectService.CreateProject(_tempDir, "Orphan Conversion Book", metadata);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        _projectService.SaveProject(project);

        var markdownPath = Path.Combine(project.ChaptersDir, "1. Legacy Chapter.md");
        File.WriteAllText(markdownPath, "Legacy markdown content.");
        var htmlPath = Path.Combine(project.ChaptersDir, "2. Web Chapter.html");
        File.WriteAllText(htmlPath, "<p>Web content.</p>");

        var vm = new MainWindowViewModel(project, _appSettingsService, _templateService);

        var chapters = vm.CurrentProject.Spine.Where(i => i.Type == SpineItemType.Chapter).OrderBy(i => i.Order).ToList();
        Assert.Equal(["Legacy Chapter", "Web Chapter"], chapters.Select(c => c.Title));
        Assert.All(chapters, c => Assert.EndsWith(".ebhtml", c.RelativePath));

        Assert.False(File.Exists(markdownPath));
        Assert.False(File.Exists(htmlPath));

        var (_, webBody) = _chapterFileService.ReadChapter(vm.CurrentProject.ResolvePath(chapters[1]));
        Assert.Contains("Web content.", webBody);
    }

    [Fact]
    public void DeleteChapter_RemovesFromSpineAndDeletesFile()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);
        var path = vm.CurrentProject.ResolvePath(chapter);

        vm.DeleteChapter(chapter);

        Assert.DoesNotContain(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteChapter_WhenDeletedChapterWasOpen_SwitchesEditorToTitlePage()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);

        vm.DeleteChapter(chapter);

        Assert.False(vm.IsChapterSelected);
        Assert.Contains(vm.CurrentProject.Metadata.Title, vm.Editor.CurrentText);
    }

    [Fact]
    public void ExportChapterAsWord_WritesDocxFileToOutputDir()
    {
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);

        vm.ExportChapterAsWord(chapter);

        var files = Directory.GetFiles(vm.CurrentProject.OutputDir, "*.docx");
        Assert.Single(files);
        Assert.Contains(vm.CurrentProject.OutputDir, vm.StatusMessage);
    }

    [Fact]
    public void ExportChapterAsWord_RendersTheChaptersTitleAsAHeading()
    {
        // A brand-new chapter's body is empty — this only passes if ExportChapterAsWord
        // actually synthesizes a heading from the spine item (see ChapterHeadingHtml), since
        // the title itself lives only in front matter, never in the body.
        var vm = NewViewModel();
        vm.AddChapterCommand.Execute(null);
        var chapter = Assert.Single(vm.CurrentProject.Spine, i => i.Type == SpineItemType.Chapter);

        vm.ExportChapterAsWord(chapter);

        var outputPath = Directory.GetFiles(vm.CurrentProject.OutputDir, "*.docx").Single();
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(outputPath, false);
        var bodyText = document.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("Chapter 1: New Chapter", bodyText);
    }
}

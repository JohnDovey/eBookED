using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Creates a brand-new eBookEditor project on disk from a source EPUB — the "Create Project
/// from ePub" command's actual work, driven by EpubImportService's parsed result. Follows the
/// same create-project-then-populate-spine shape MainWindowViewModel.ImportDocx/
/// ImportChapterFiles use for importing into an already-open project, just running before any
/// project (or MainWindowViewModel) exists yet — see SpineImportRouter, the shared dispatch
/// logic both this class and MainWindowViewModel call.
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

    public EpubProjectImporter()
    {
        _spineImportRouter = new SpineImportRouter(_spineService);
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

        foreach (var draft in result.Items)
        {
            var path = _chapterFileService.CreateNewChapterFile(SpineImportRouter.ChapterDirFor(project, draft.Type), draft.Title);
            _chapterFileService.WriteChapter(path, new ChapterFrontMatter { Title = draft.Title }, draft.Body);
            var relativePath = Path.GetRelativePath(project.DirectoryPath, path).Replace('\\', '/');
            _spineImportRouter.AddImportedItemToSpine(project, draft.Title, relativePath, draft.Type, draft.NumberMode);

            foreach (var image in draft.Images)
                File.WriteAllBytes(Path.Combine(project.ImagesDir, image.FileName), image.Bytes);
        }

        if (result.CoverImageBytes is { } coverBytes && result.CoverImageFileName is { Length: > 0 } coverFileName)
        {
            File.WriteAllBytes(Path.Combine(project.ImagesDir, coverFileName), coverBytes);
            project.ProjectFile.Metadata = project.Metadata with { CoverImagePath = $"{ProjectPaths.ImagesDirName}/{coverFileName}" };
        }

        _chapterFileService.SyncChapterFileNames(project);
        _pageGenerator.RegenerateAllGeneratedPages(project);
        File.WriteAllText(project.BookMdPath, _bookIndexGenerator.GenerateBookMd(project));
        _projectService.SaveProject(project);

        return project;
    }
}

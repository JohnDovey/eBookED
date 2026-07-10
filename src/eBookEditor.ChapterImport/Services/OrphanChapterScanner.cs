using eBookEditor.Core.Models;

namespace eBookEditor.ChapterImport.Services;

/// <summary>
/// Finds chapter-shaped files sitting in a project's chapters/ directory that aren't
/// referenced by any spine item — e.g. dropped into the folder directly via Finder/Explorer
/// rather than through the app. Covers this app's own native .ebhtml format as well as every
/// extension ChapterImportService.ImportFile otherwise accepts (.md, .docx, .html, .htm), so a
/// manuscript file dropped straight into chapters/ gets picked up regardless of its original
/// format. Callers run each result through ChapterImportService.ImportFile the same as any
/// other imported file — see MainWindowViewModel.ImportOrphanedChapterFiles, which also
/// silently converts every non-.ebhtml orphan to a native .ebhtml chapter file and deletes the
/// original once its content is safely written into the project.
/// </summary>
public class OrphanChapterScanner
{
    private static readonly string[] SupportedExtensions = ["*.ebhtml", "*.md", "*.docx", "*.html", "*.htm"];

    public IReadOnlyList<string> FindOrphanedChapterFiles(EbookProject project)
    {
        if (!Directory.Exists(project.ChaptersDir))
            return [];

        var referencedPaths = project.Spine
            .Select(i => Path.GetFullPath(project.ResolvePath(i)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SupportedExtensions
            .SelectMany(pattern => Directory.EnumerateFiles(project.ChaptersDir, pattern))
            .Where(path => !referencedPaths.Contains(Path.GetFullPath(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

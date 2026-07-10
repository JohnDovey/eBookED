using eBookEditor.Core.Models;

namespace eBookEditor.ChapterImport.Services;

/// <summary>
/// Finds .ebhtml files sitting in a project's chapters/ directory that aren't referenced by
/// any spine item — e.g. dropped into the folder directly via Finder/Explorer rather than
/// through the app. Callers run each result through ChapterImportService.ImportFile to add
/// it to the spine, the same as any other imported file.
/// </summary>
public class OrphanChapterScanner
{
    public IReadOnlyList<string> FindOrphanedChapterFiles(EbookProject project)
    {
        if (!Directory.Exists(project.ChaptersDir))
            return [];

        var referencedPaths = project.Spine
            .Select(i => Path.GetFullPath(project.ResolvePath(i)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(project.ChaptersDir, "*.ebhtml")
            .Where(path => !referencedPaths.Contains(Path.GetFullPath(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

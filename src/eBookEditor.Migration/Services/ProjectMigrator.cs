using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Migration.Services;

public record MigrationResult(int ConvertedFileCount);

/// <summary>
/// Upgrades a legacy Markdown-bodied project (chapters/front/back matter stored as ".md" files)
/// to this app's native HTML format (".ebhtml") — the one-time batch conversion tool for
/// projects created before the HTML content-model refactor. User-triggered only ("Upgrade
/// Project to HTML…"), never silent/automatic.
///
/// Every spine item's Markdown body is converted to HTML verbatim via Markdig
/// (MarkdownToHtmlConverter) — including the auto-generated front/back matter pages (title,
/// imprint, TOC, about-author). Those are deliberately NOT regenerated from metadata here (the
/// way PageGeneratorService.RegenerateAllGeneratedPages would): a hand-edited generated page is
/// exactly as likely to carry real authorial changes as a hand-written chapter, and silently
/// discarding that on upgrade would be a real, surprising data loss.
/// </summary>
public class ProjectMigrator
{
    private readonly ChapterFileService _chapterFileService = new();
    private readonly ProjectService _projectService = new();
    private readonly MarkdownToHtmlConverter _htmlConverter = new();
    private readonly BookIndexGenerator _bookIndexGenerator = new();

    /// <summary>
    /// True if any spine item still points at a legacy ".md" file. Deliberately not based on
    /// ProjectFile.SchemaVersion: a project.ebookproj.json saved by an app version that
    /// predates the SchemaVersion field simply has no "schemaVersion" key in its JSON, and
    /// System.Text.Json fills an absent init-property from its C# default — which for
    /// SchemaVersion is CurrentSchemaVersion, not the legacy value. Trusting the field directly
    /// would make a genuinely legacy project look already-migrated. The actual file extensions
    /// on disk are the only ground truth.
    /// </summary>
    public bool NeedsMigration(EbookProject project) =>
        project.Spine.Any(item => IsLegacyPath(item.RelativePath));

    /// <summary>Copies the whole project directory to a sibling "{name} (Backup before HTML
    /// Upgrade)" folder, returning its path. Call before MigrateToHtml, which mutates the
    /// project in place.</summary>
    public string CreateBackup(EbookProject project)
    {
        var parentDir = Path.GetDirectoryName(project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            ?? throw new InvalidOperationException("Could not determine the project's parent directory.");
        var projectName = Path.GetFileName(project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var backupDir = Path.Combine(parentDir, $"{projectName} (Backup before HTML Upgrade)");
        var uniqueBackupDir = backupDir;
        var suffix = 2;
        while (Directory.Exists(uniqueBackupDir))
        {
            uniqueBackupDir = $"{backupDir} {suffix}";
            suffix++;
        }

        CopyDirectory(project.DirectoryPath, uniqueBackupDir);
        return uniqueBackupDir;
    }

    /// <summary>
    /// Converts every legacy spine item's Markdown body to HTML, writes it out as a new
    /// ".ebhtml" file, deletes the old ".md" file, updates the spine, regenerates the internal
    /// book.ebhtml navigation index, and saves the project. Idempotent — a no-op (returns a
    /// result with ConvertedFileCount 0) if the project has already been migrated, so it's safe
    /// to call without the caller re-checking NeedsMigration first.
    /// </summary>
    public MigrationResult MigrateToHtml(EbookProject project)
    {
        if (!NeedsMigration(project))
            return new MigrationResult(ConvertedFileCount: 0);

        var convertedCount = 0;
        for (var i = 0; i < project.Spine.Count; i++)
        {
            var item = project.Spine[i];
            if (!IsLegacyPath(item.RelativePath))
                continue;

            var oldPath = project.ResolvePath(item);
            var (frontMatter, markdownBody) = _chapterFileService.ParseChapter(File.ReadAllText(oldPath));
            var htmlBody = _htmlConverter.ToHtml(markdownBody);

            var newRelativePath = Path.ChangeExtension(item.RelativePath, ".ebhtml").Replace('\\', '/');
            var newPath = Path.Combine(project.DirectoryPath, newRelativePath);
            _chapterFileService.WriteChapter(newPath, frontMatter, htmlBody);
            File.Delete(oldPath);

            project.Spine[i] = item with { RelativePath = newRelativePath };
            convertedCount++;
        }

        project.ProjectFile = project.ProjectFile with { SchemaVersion = ProjectFile.CurrentSchemaVersion };

        // book.ebhtml is always auto-regenerated whenever the spine changes (see
        // BookIndexGenerator's own doc comment) — safe, and correct, to just regenerate it
        // fresh with the new HTML generator rather than converting its old "book.md" content.
        File.WriteAllText(project.BookMdPath, _bookIndexGenerator.GenerateBookMd(project));
        var legacyBookMdPath = Path.Combine(project.DirectoryPath, "book.md");
        if (File.Exists(legacyBookMdPath))
            File.Delete(legacyBookMdPath);

        _projectService.SaveProject(project);
        _chapterFileService.SyncChapterFileNames(project);
        _projectService.SaveProject(project);

        return new MigrationResult(convertedCount);
    }

    private static bool IsLegacyPath(string relativePath) =>
        Path.GetExtension(relativePath).Equals(".md", StringComparison.OrdinalIgnoreCase);

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
            File.Copy(filePath, Path.Combine(destinationDir, Path.GetFileName(filePath)));

        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
            CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
    }
}

using System.Text.Json;
using eBookEditor.Core.Models;
using eBookEditor.Core.Serialization;

namespace eBookEditor.Core.Services;

public class ProjectService
{
    private readonly JsonSerializerOptions _jsonOptions = JsonOptions.Create();

    public EbookProject CreateProject(string parentDir, string projectName, BookMetadata initialMetadata)
    {
        var safeName = SanitizeProjectName(projectName);
        var dir = Path.Combine(parentDir, safeName);
        if (Directory.Exists(dir))
            throw new InvalidOperationException($"A project already exists at '{dir}'.");

        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ProjectPaths.FrontMatterDirName));
        Directory.CreateDirectory(Path.Combine(dir, ProjectPaths.ChaptersDirName));
        Directory.CreateDirectory(Path.Combine(dir, ProjectPaths.BackMatterDirName));
        Directory.CreateDirectory(Path.Combine(dir, ProjectPaths.ImagesDirName));
        Directory.CreateDirectory(Path.Combine(dir, ProjectPaths.OutputDirName));

        var spine = new List<SpineItem>
        {
            new()
            {
                Type = SpineItemType.FrontMatter,
                RelativePath = $"{ProjectPaths.FrontMatterDirName}/{ProjectPaths.TitlePageFileName}",
                IsGenerated = true,
                Title = "Title Page",
                Order = 0
            },
            new()
            {
                Type = SpineItemType.FrontMatter,
                RelativePath = $"{ProjectPaths.FrontMatterDirName}/{ProjectPaths.CopyrightPageFileName}",
                IsGenerated = true,
                Title = "Copyright",
                Order = 1
            },
            new()
            {
                Type = SpineItemType.FrontMatter,
                RelativePath = $"{ProjectPaths.FrontMatterDirName}/{ProjectPaths.TocPageFileName}",
                IsGenerated = true,
                Title = "Table of Contents",
                Order = 2
            },
            new()
            {
                Type = SpineItemType.BackMatter,
                RelativePath = $"{ProjectPaths.BackMatterDirName}/{ProjectPaths.AboutAuthorPageFileName}",
                IsGenerated = true,
                Title = "About the Author",
                Order = 3
            }
        };

        foreach (var item in spine)
        {
            var path = Path.Combine(dir, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(path, string.Empty);
        }

        var projectFile = new ProjectFile
        {
            Metadata = initialMetadata,
            Spine = spine
        };

        var project = new EbookProject { DirectoryPath = dir, ProjectFile = projectFile };
        SaveProject(project);
        File.WriteAllText(project.BookMdPath, $"# {initialMetadata.Title}\n");
        return project;
    }

    public EbookProject LoadProject(string projectDir)
    {
        var projectFilePath = Path.Combine(projectDir, ProjectPaths.ProjectFileName);
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException($"No project file found at '{projectFilePath}'.");

        var json = File.ReadAllText(projectFilePath);
        var projectFile = JsonSerializer.Deserialize<ProjectFile>(json, _jsonOptions)
            ?? throw new InvalidDataException($"Project file at '{projectFilePath}' could not be parsed.");

        var project = new EbookProject { DirectoryPath = projectDir, ProjectFile = projectFile };
        foreach (var item in project.Spine)
        {
            var path = project.ResolvePath(item);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Spine item '{item.RelativePath}' referenced in project file is missing on disk.");
        }
        return project;
    }

    public void SaveProject(EbookProject project)
    {
        project.ProjectFile.ModifiedUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(project.ProjectFile, _jsonOptions);
        File.WriteAllText(project.ProjectFilePath, json);
    }

    private static string SanitizeProjectName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Project name must contain at least one valid character.", nameof(name));
        return sanitized;
    }
}

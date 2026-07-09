using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.App.Services;

/// <summary>
/// Creates (or loads, if already present) a demo project on first run. A full New Project
/// Wizard / Open Project flow is future work; this keeps the app usable out of the box
/// in the meantime, isolated behind a static factory so ViewModels stay testable with any
/// <see cref="EbookProject"/> instead of always bootstrapping this fixed sample.
/// </summary>
public static class SampleProjectFactory
{
    public static EbookProject LoadOrCreate(string baseDirectory)
    {
        var projectService = new ProjectService();
        var sampleRoot = Path.Combine(baseDirectory, "sample-project-data");
        Directory.CreateDirectory(sampleRoot);
        var projectDir = Path.Combine(sampleRoot, "My Sample Book");

        if (Directory.Exists(projectDir))
            return projectService.LoadProject(projectDir);

        var metadata = new BookMetadata
        {
            Title = "My Sample Book",
            Subtitle = "An eBook Editor Demo",
            Contributors = [new Contributor("Jane", "Author", ContributorRole.Author)],
            CopyrightHolder = "Jane Author",
            CopyrightYear = DateTime.UtcNow.Year,
            Language = "en",
            Blurb = "A demonstration book created automatically by eBook Editor."
        };

        var project = projectService.CreateProject(sampleRoot, "My Sample Book", metadata);
        var pageGenerator = new PageGeneratorService();
        pageGenerator.RegenerateAllGeneratedPages(project);
        File.WriteAllText(project.BookMdPath, new BookIndexGenerator().GenerateBookMd(project));
        return project;
    }
}

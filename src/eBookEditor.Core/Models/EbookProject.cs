namespace eBookEditor.Core.Models;

public class EbookProject
{
    public required string DirectoryPath { get; init; }
    public required ProjectFile ProjectFile { get; set; }

    public string ProjectFilePath => Path.Combine(DirectoryPath, ProjectPaths.ProjectFileName);
    public string BookMdPath => Path.Combine(DirectoryPath, ProjectPaths.BookMdFileName);
    public string FrontMatterDir => Path.Combine(DirectoryPath, ProjectPaths.FrontMatterDirName);
    public string ChaptersDir => Path.Combine(DirectoryPath, ProjectPaths.ChaptersDirName);
    public string BackMatterDir => Path.Combine(DirectoryPath, ProjectPaths.BackMatterDirName);
    public string ImagesDir => Path.Combine(DirectoryPath, ProjectPaths.ImagesDirName);
    public string OutputDir => Path.Combine(DirectoryPath, ProjectPaths.OutputDirName);

    public BookMetadata Metadata => ProjectFile.Metadata;
    public List<SpineItem> Spine => ProjectFile.Spine;

    public string ResolvePath(SpineItem item) => Path.Combine(DirectoryPath, item.RelativePath);
}

public static class ProjectPaths
{
    public const string ProjectFileName = "project.ebookproj.json";
    // ".ebhtml" (not ".md") from the HTML content-model refactor onward — the body is an HTML
    // fragment plus a YAML front-matter header, not standalone valid HTML/XML on its own, so a
    // distinct extension keeps it from being mistaken for either a plain Markdown or a plain
    // HTML file. Legacy ".md" projects are gated behind ProjectFile.SchemaVersion instead of
    // silently treated as interchangeable — see the migration tool (Phase 6 of the refactor).
    public const string BookMdFileName = "book.ebhtml";
    public const string FrontMatterDirName = "frontmatter";
    public const string ChaptersDirName = "chapters";
    public const string BackMatterDirName = "backmatter";
    public const string ImagesDirName = "images";
    public const string OutputDirName = "output";

    public const string TitlePageFileName = "title-page.ebhtml";
    public const string CopyrightPageFileName = "copyright.ebhtml";
    public const string TocPageFileName = "toc.ebhtml";
    public const string AboutAuthorPageFileName = "about-the-author.ebhtml";
    public const string IndexPageFileName = "index.ebhtml";
}

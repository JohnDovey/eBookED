using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Markdown.Services;

namespace eBookEditor.Tests.Markdown;

public class PageGeneratorServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _projectService = new();
    private readonly SpineService _spineService = new();
    private readonly PageGeneratorService _pageGenerator = new();

    public PageGeneratorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BookMetadata SampleMetadata() => new()
    {
        Title = "The Great Novel",
        Subtitle = "A Story",
        Contributors =
        [
            new Contributor("Jane", "Doe", ContributorRole.Author),
            new Contributor("John", "Smith", ContributorRole.Editor)
        ],
        CopyrightHolder = "Jane Doe",
        CopyrightYear = 2026,
        Publisher = new PublisherInfo("Acme Press"),
        Isbn13 = "9780306406157",
        AboutAuthor = new AboutAuthorInfo
        {
            Bio = "Jane Doe writes speculative fiction.",
            PhotoPath = "images/author.jpg",
            SocialLinks = [new SocialLink("Twitter", "https://twitter.com/janedoe")]
        }
    };

    [Fact]
    public void GenerateTitlePage_IncludesTitleSubtitleAndAuthors()
    {
        var markdown = _pageGenerator.GenerateTitlePage(SampleMetadata());

        Assert.Contains("# The Great Novel", markdown);
        Assert.Contains("## A Story", markdown);
        Assert.Contains("*by Jane Doe*", markdown);
        Assert.Contains("Edited by John Smith", markdown);
    }

    [Fact]
    public void GenerateCopyrightPage_IncludesYearHolderAndIsbn()
    {
        var markdown = _pageGenerator.GenerateCopyrightPage(SampleMetadata());

        Assert.Contains("Copyright © 2026 Jane Doe", markdown);
        Assert.Contains("Published by Acme Press", markdown);
        Assert.Contains("ISBN-13: 9780306406157", markdown);
        Assert.Contains(BookMetadata.DefaultDisclaimerText, markdown);
    }

    [Fact]
    public void GenerateCopyrightPage_IsAnImprintPageWithCoverAndContributorsNearTopAndCopyrightAtBottom()
    {
        var metadata = SampleMetadata() with { CoverImagePath = "images/cover.jpg" };

        var markdown = _pageGenerator.GenerateCopyrightPage(metadata);

        Assert.Contains("![Cover](../images/cover.jpg)", markdown);
        Assert.Contains("By Jane Doe", markdown);
        Assert.Contains("Edited by John Smith", markdown);

        var coverIndex = markdown.IndexOf("![Cover]", StringComparison.Ordinal);
        var byLineIndex = markdown.IndexOf("By Jane Doe", StringComparison.Ordinal);
        var isbnIndex = markdown.IndexOf("ISBN-13:", StringComparison.Ordinal);
        var copyrightIndex = markdown.IndexOf("Copyright ©", StringComparison.Ordinal);
        var disclaimerIndex = markdown.IndexOf(BookMetadata.DefaultDisclaimerText, StringComparison.Ordinal);

        Assert.True(coverIndex < byLineIndex, "Cover thumbnail should come before contributor names.");
        Assert.True(byLineIndex < isbnIndex, "Contributors should come before ISBN/publisher details.");
        Assert.True(isbnIndex < copyrightIndex, "Publisher/ISBN details should come before the copyright statement.");
        Assert.True(copyrightIndex < disclaimerIndex, "Copyright statement should come immediately before the disclaimer, both at the bottom.");
    }

    [Fact]
    public void GenerateAboutAuthorPage_IncludesBioPhotoAndSocialLinks()
    {
        var markdown = _pageGenerator.GenerateAboutAuthorPage(SampleMetadata());

        Assert.Contains("# About the Author", markdown);
        Assert.Contains("![Author photo](../images/author.jpg)", markdown);
        Assert.Contains("Jane Doe writes speculative fiction.", markdown);
        Assert.Contains("[Twitter](https://twitter.com/janedoe)", markdown);
    }

    [Fact]
    public void GenerateAboutAuthorPage_OmitsConnectSectionWhenNoSocialLinks()
    {
        var metadata = SampleMetadata() with
        {
            AboutAuthor = new AboutAuthorInfo { Bio = "Just a bio." }
        };

        var markdown = _pageGenerator.GenerateAboutAuthorPage(metadata);

        Assert.DoesNotContain("## Connect", markdown);
    }

    [Fact]
    public void GenerateTocPage_ListsChaptersWithResolvedNumbersAndExcludesTocItself()
    {
        var project = _projectService.CreateProject(_tempDir, "Toc Test", SampleMetadata());
        // Real chapter filenames follow "NNN - Title.md" (see ChapterFileNaming.BuildFileName)
        // and contain spaces — the link destination must be angle-bracket-wrapped for this to
        // parse as an actual Markdown link at all; a space-free test fixture wouldn't catch a
        // regression here (see EpubBuilderTests for the exported-EPUB-level regression test).
        File.WriteAllText(Path.Combine(project.ChaptersDir, "001 - The Beginning.md"), "One");
        _spineService.AddChapter(project, "The Beginning", "chapters/001 - The Beginning.md");

        var markdown = _pageGenerator.GenerateTocPage(project.Spine);

        Assert.Contains("- [Chapter 1: The Beginning](<chapters/001 - The Beginning.md>)", markdown);
        Assert.DoesNotContain(ProjectPaths.TocPageFileName, markdown);
    }

    [Fact]
    public void RegenerateAllGeneratedPages_WritesAllFourFiles()
    {
        var project = _projectService.CreateProject(_tempDir, "Regen Test", SampleMetadata());

        _pageGenerator.RegenerateAllGeneratedPages(project);

        var titleText = File.ReadAllText(Path.Combine(project.FrontMatterDir, ProjectPaths.TitlePageFileName));
        var copyrightText = File.ReadAllText(Path.Combine(project.FrontMatterDir, ProjectPaths.CopyrightPageFileName));
        var tocText = File.ReadAllText(Path.Combine(project.FrontMatterDir, ProjectPaths.TocPageFileName));
        var aboutText = File.ReadAllText(Path.Combine(project.BackMatterDir, ProjectPaths.AboutAuthorPageFileName));

        Assert.Contains("The Great Novel", titleText);
        Assert.Contains("Copyright ©", copyrightText);
        Assert.Contains("Table of Contents", tocText);
        Assert.Contains("About the Author", aboutText);
    }
}

using System.Net;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class PageGeneratorServiceTests : IDisposable
{
    // BookMetadata.DefaultDisclaimerText contains an apostrophe ("author's imagination"), which
    // WebUtility.HtmlEncode (correctly) escapes to "&#39;" in generated HTML — the raw text with
    // its literal apostrophe never appears verbatim in HTML output, only its encoded form.
    private static readonly string EncodedDefaultDisclaimerText = WebUtility.HtmlEncode(BookMetadata.DefaultDisclaimerText);

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
        var html = _pageGenerator.GenerateTitlePage(SampleMetadata());

        Assert.Contains("<h1>The Great Novel</h1>", html);
        Assert.Contains("<h2>A Story</h2>", html);
        Assert.Contains("<em>by Jane Doe</em>", html);
        Assert.Contains("Edited by John Smith", html);
    }

    [Fact]
    public void GenerateTitlePage_HtmlEncodesInterpolatedValues()
    {
        var metadata = SampleMetadata() with { Title = "Foo & Bar <Baz>" };

        var html = _pageGenerator.GenerateTitlePage(metadata);

        Assert.Contains("<h1>Foo &amp; Bar &lt;Baz&gt;</h1>", html);
        Assert.DoesNotContain("<Baz>", html);
    }

    [Fact]
    public void GenerateCopyrightPage_IncludesYearHolderAndIsbn()
    {
        var html = _pageGenerator.GenerateCopyrightPage(SampleMetadata());

        Assert.Contains("Copyright © 2026 Jane Doe", html);
        Assert.Contains("Published by Acme Press", html);
        Assert.Contains("ISBN-13: 9780306406157", html);
        Assert.Contains(EncodedDefaultDisclaimerText, html);
    }

    [Fact]
    public void GenerateCopyrightPage_IsAnImprintPageWithCoverAndContributorsNearTopAndCopyrightAtBottom()
    {
        var metadata = SampleMetadata() with { CoverImagePath = "images/cover.jpg" };

        var html = _pageGenerator.GenerateCopyrightPage(metadata);

        Assert.Contains("<img src=\"../images/cover.jpg\" alt=\"Cover\">", html);
        Assert.Contains("By Jane Doe", html);
        Assert.Contains("Edited by John Smith", html);

        var coverIndex = html.IndexOf("<img", StringComparison.Ordinal);
        var byLineIndex = html.IndexOf("By Jane Doe", StringComparison.Ordinal);
        var isbnIndex = html.IndexOf("ISBN-13:", StringComparison.Ordinal);
        var copyrightIndex = html.IndexOf("Copyright ©", StringComparison.Ordinal);
        var disclaimerIndex = html.IndexOf(EncodedDefaultDisclaimerText, StringComparison.Ordinal);

        Assert.True(coverIndex < byLineIndex, "Cover thumbnail should come before contributor names.");
        Assert.True(byLineIndex < isbnIndex, "Contributors should come before ISBN/publisher details.");
        Assert.True(isbnIndex < copyrightIndex, "Publisher/ISBN details should come before the copyright statement.");
        Assert.True(copyrightIndex < disclaimerIndex, "Copyright statement should come immediately before the disclaimer, both at the bottom.");
    }

    [Fact]
    public void GenerateAboutAuthorPage_IncludesBioPhotoAndSocialLinks()
    {
        var html = _pageGenerator.GenerateAboutAuthorPage(SampleMetadata());

        Assert.Contains("<h1>About the Author</h1>", html);
        Assert.Contains("<img src=\"../images/author.jpg\" alt=\"Author photo\">", html);
        Assert.Contains("Jane Doe writes speculative fiction.", html);
        Assert.Contains("<a href=\"https://twitter.com/janedoe\">Twitter</a>", html);
    }

    [Fact]
    public void GenerateAboutAuthorPage_OmitsConnectSectionWhenNoSocialLinks()
    {
        var metadata = SampleMetadata() with
        {
            AboutAuthor = new AboutAuthorInfo { Bio = "Just a bio." }
        };

        var html = _pageGenerator.GenerateAboutAuthorPage(metadata);

        Assert.DoesNotContain("<h2>Connect</h2>", html);
    }

    [Fact]
    public void GenerateAboutAuthorPage_SplitsBioOnBlankLinesIntoSeparateParagraphs()
    {
        var metadata = SampleMetadata() with
        {
            AboutAuthor = new AboutAuthorInfo { Bio = "First paragraph.\n\nSecond paragraph." }
        };

        var html = _pageGenerator.GenerateAboutAuthorPage(metadata);

        Assert.Contains("<p>First paragraph.</p>", html);
        Assert.Contains("<p>Second paragraph.</p>", html);
    }

    [Fact]
    public void GenerateTocPage_ListsChaptersWithResolvedNumbersAndExcludesTocItself()
    {
        var project = _projectService.CreateProject(_tempDir, "Toc Test", SampleMetadata());
        // Real chapter filenames follow "NNN - Title.ebhtml" (see ChapterFileNaming.BuildFileName)
        // and contain spaces — unlike Markdown link syntax, an HTML href attribute handles
        // spaces without any special escaping, so no angle-bracket workaround is needed here.
        File.WriteAllText(Path.Combine(project.ChaptersDir, "001 - The Beginning.ebhtml"), "One");
        _spineService.AddChapter(project, "The Beginning", "chapters/001 - The Beginning.ebhtml");

        var html = _pageGenerator.GenerateTocPage(project.Spine);

        Assert.Contains("<li><a href=\"chapters/001 - The Beginning.ebhtml\">Chapter 1: The Beginning</a></li>", html);
        Assert.DoesNotContain(ProjectPaths.TocPageFileName, html);
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

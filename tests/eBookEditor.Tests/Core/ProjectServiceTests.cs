using eBookEditor.Core.Models;
using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class ProjectServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProjectService _service = new();

    public ProjectServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CreateProject_ScaffoldsDirectoryStructure()
    {
        var metadata = new BookMetadata { Title = "My Test Book" };

        var project = _service.CreateProject(_tempDir, "My Test Book", metadata);

        Assert.True(Directory.Exists(project.FrontMatterDir));
        Assert.True(Directory.Exists(project.ChaptersDir));
        Assert.True(Directory.Exists(project.BackMatterDir));
        Assert.True(Directory.Exists(project.ImagesDir));
        Assert.True(Directory.Exists(project.OutputDir));
        Assert.True(File.Exists(project.ProjectFilePath));
        Assert.True(File.Exists(project.BookMdPath));
    }

    [Fact]
    public void CreateProject_SeedsGeneratedFrontAndBackMatterSpineItems()
    {
        var metadata = new BookMetadata { Title = "Spine Test" };

        var project = _service.CreateProject(_tempDir, "Spine Test", metadata);

        Assert.Equal(4, project.Spine.Count);
        Assert.Equal(3, project.Spine.Count(i => i.Type == SpineItemType.FrontMatter));
        Assert.Single(project.Spine, i => i.Type == SpineItemType.BackMatter);
        Assert.All(project.Spine, i => Assert.True(i.IsGenerated));
        Assert.Equal("About the Author", project.Spine.Single(i => i.Type == SpineItemType.BackMatter).Title);
    }

    [Fact]
    public void CreateProject_ThrowsWhenDirectoryAlreadyExists()
    {
        var metadata = new BookMetadata { Title = "Dup" };
        _service.CreateProject(_tempDir, "Dup", metadata);

        Assert.Throws<InvalidOperationException>(() => _service.CreateProject(_tempDir, "Dup", metadata));
    }

    [Fact]
    public void SaveAndLoadProject_RoundTripsMetadataAndSpine()
    {
        var metadata = new BookMetadata
        {
            Title = "Round Trip",
            Subtitle = "A Subtitle",
            Contributors = [new Contributor("Jane Doe", ContributorRole.Author)],
            CopyrightHolder = "Jane Doe",
            CopyrightYear = 2026,
            Publisher = new PublisherInfo("Acme Press", "images/logo.png"),
            PublicationDate = new DateOnly(2026, 7, 8),
            Language = "en",
            GenreTags = ["Sci-Fi"],
            FreeTags = ["debut"],
            Blurb = "A gripping tale.",
            Isbn10 = "0306406152",
            Isbn13 = "9780306406157",
            AboutAuthor = new AboutAuthorInfo
            {
                Bio = "Jane writes books.",
                PhotoPath = "images/author.jpg",
                SocialLinks = [new SocialLink("Twitter", "https://twitter.com/jane")]
            },
            StoreLinks = [new StoreLink(StoreName.KindleStore, "https://amazon.com/dp/xyz")]
        };

        var created = _service.CreateProject(_tempDir, "Round Trip", metadata);
        var loaded = _service.LoadProject(created.DirectoryPath);

        Assert.Equal(metadata.Title, loaded.Metadata.Title);
        Assert.Equal(metadata.Subtitle, loaded.Metadata.Subtitle);
        Assert.Equal(metadata.Isbn13, loaded.Metadata.Isbn13);
        Assert.Equal(metadata.PublicationDate, loaded.Metadata.PublicationDate);
        Assert.Single(loaded.Metadata.Contributors);
        Assert.Equal("Jane Doe", loaded.Metadata.Authors.Single().Name);
        Assert.Equal("Jane writes books.", loaded.Metadata.AboutAuthor!.Bio);
        Assert.Equal(4, loaded.Spine.Count);
    }

    [Fact]
    public void LoadProject_ThrowsWhenSpineFileMissing()
    {
        var metadata = new BookMetadata { Title = "Missing File" };
        var project = _service.CreateProject(_tempDir, "Missing File", metadata);
        File.Delete(Path.Combine(project.DirectoryPath, project.Spine[0].RelativePath));

        Assert.Throws<FileNotFoundException>(() => _service.LoadProject(project.DirectoryPath));
    }
}

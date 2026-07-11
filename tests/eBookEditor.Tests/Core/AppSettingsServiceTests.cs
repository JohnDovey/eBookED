using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Tests.App;

namespace eBookEditor.Tests.Core;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppSettingsService _service;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ebookeditor-tests-" + Guid.NewGuid());
        _service = new AppSettingsService(new TestAppPaths(_tempDir));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RecordProjectOpened_AddsToRecentAndOpenProjectPaths()
    {
        var settings = _service.RecordProjectOpened("/projects/BookOne");

        Assert.Contains("/projects/BookOne", settings.RecentProjectPaths);
        Assert.Contains("/projects/BookOne", settings.OpenProjectPaths);
    }

    [Fact]
    public void RecordProjectOpened_MovesExistingPathToFrontOfRecentWithoutDuplicating()
    {
        _service.RecordProjectOpened("/projects/BookOne");
        _service.RecordProjectOpened("/projects/BookTwo");
        var settings = _service.RecordProjectOpened("/projects/BookOne");

        Assert.Equal(["/projects/BookOne", "/projects/BookTwo"], settings.RecentProjectPaths);
    }

    [Fact]
    public void RecordProjectOpened_DoesNotDuplicateInOpenProjectPaths()
    {
        _service.RecordProjectOpened("/projects/BookOne");
        var settings = _service.RecordProjectOpened("/projects/BookOne");

        Assert.Single(settings.OpenProjectPaths);
    }

    [Fact]
    public void RecordProjectOpened_CapsRecentProjectPathsAtTen()
    {
        AppSettings? settings = null;
        for (var i = 0; i < 15; i++)
            settings = _service.RecordProjectOpened($"/projects/Book{i}");

        Assert.Equal(10, settings!.RecentProjectPaths.Count);
        Assert.Equal("/projects/Book14", settings.RecentProjectPaths[0]);
    }

    [Fact]
    public void RecordProjectClosed_RemovesFromOpenProjectPathsButKeepsRecent()
    {
        _service.RecordProjectOpened("/projects/BookOne");

        var settings = _service.RecordProjectClosed("/projects/BookOne");

        Assert.DoesNotContain("/projects/BookOne", settings.OpenProjectPaths);
        Assert.Contains("/projects/BookOne", settings.RecentProjectPaths);
    }

    [Fact]
    public void Load_CorruptSettingsFile_FallsBackToFreshSettingsInsteadOfThrowing()
    {
        var paths = new TestAppPaths(_tempDir);
        Directory.CreateDirectory(paths.AppDataDirectory);
        File.WriteAllText(paths.SettingsFilePath, "{ this is not valid json");

        var settings = _service.Load();

        Assert.Empty(settings.RecentProjectPaths);
    }

    [Fact]
    public void Save_DoesNotLeaveATempFileBehind()
    {
        var paths = new TestAppPaths(_tempDir);
        _service.RecordProjectOpened("/projects/BookOne");

        var leftoverTempFiles = Directory.GetFiles(paths.AppDataDirectory, "*.tmp");

        Assert.Empty(leftoverTempFiles);
    }
}

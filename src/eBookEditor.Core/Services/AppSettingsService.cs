using System.Text.Json;
using eBookEditor.Core.Models;
using eBookEditor.Core.Serialization;

namespace eBookEditor.Core.Services;

public class AppSettingsService
{
    private readonly IAppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = JsonOptions.Create();

    public AppSettingsService(IAppPaths paths)
    {
        _paths = paths;
    }

    /// <summary>Falls back to a fresh, empty AppSettings on any read/parse failure (missing
    /// file, corrupt/truncated JSON) rather than throwing — this is read on every menu open
    /// (see MainWindow.OnRecentProjectsSubmenuOpened, which clears the menu's items before
    /// calling this), so an uncaught exception here would leave the Recent Projects menu
    /// permanently empty instead of showing either real entries or the "no recent projects"
    /// fallback text.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(_paths.SettingsFilePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_paths.SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Writes to a temp file and renames it into place rather than writing the real
    /// settings file directly — a mid-write crash (or another window's own Save racing this
    /// one, since every MainWindowViewModel holds its own AppSettingsService instance over the
    /// same file with no locking) can otherwise leave a truncated, unparseable JSON file behind
    /// for Load() to trip over next time.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.AppDataDirectory);
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var tempPath = $"{_paths.SettingsFilePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _paths.SettingsFilePath, overwrite: true);
    }

    public AppSettings RecordProjectOpened(string projectDir)
    {
        var settings = Load();
        var recentPaths = settings.RecentProjectPaths
            .Where(p => p != projectDir)
            .Prepend(projectDir)
            .Take(10)
            .ToList();
        var openPaths = settings.OpenProjectPaths.Contains(projectDir)
            ? settings.OpenProjectPaths
            : settings.OpenProjectPaths.Append(projectDir).ToList();
        var updated = settings with { RecentProjectPaths = recentPaths, OpenProjectPaths = openPaths };
        Save(updated);
        return updated;
    }

    /// <summary>Removes a project from the "currently open windows" list recorded on close,
    /// so the next launch only restores windows that were actually still open.</summary>
    public AppSettings RecordProjectClosed(string projectDir)
    {
        var settings = Load();
        var updated = settings with { OpenProjectPaths = settings.OpenProjectPaths.Where(p => p != projectDir).ToList() };
        Save(updated);
        return updated;
    }

    public AppSettings RecordContributorUsed(string name, ContributorRole role)
    {
        var settings = Load();

        List<string> Merge(List<string> existing) => existing
            .Where(n => !string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            .Prepend(name)
            .Take(50)
            .ToList();

        var updated = role switch
        {
            ContributorRole.Author => settings with { KnownAuthorNames = Merge(settings.KnownAuthorNames) },
            ContributorRole.Editor => settings with { KnownEditorNames = Merge(settings.KnownEditorNames) },
            ContributorRole.Illustrator => settings with { KnownIllustratorNames = Merge(settings.KnownIllustratorNames) },
            _ => settings
        };
        Save(updated);
        return updated;
    }

    public AppSettings RecordPublisherUsed(PublisherInfo publisher)
    {
        var settings = Load();
        var merged = settings.KnownPublishers
            .Where(p => !string.Equals(p.Name, publisher.Name, StringComparison.OrdinalIgnoreCase))
            .Prepend(publisher)
            .Take(20)
            .ToList();
        var updated = settings with { KnownPublishers = merged };
        Save(updated);
        return updated;
    }
}

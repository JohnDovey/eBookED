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

    public AppSettings Load()
    {
        if (!File.Exists(_paths.SettingsFilePath))
            return new AppSettings();

        var json = File.ReadAllText(_paths.SettingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.AppDataDirectory);
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_paths.SettingsFilePath, json);
    }

    public AppSettings RecordProjectOpened(string projectDir)
    {
        var settings = Load();
        var paths = settings.RecentProjectPaths
            .Where(p => p != projectDir)
            .Prepend(projectDir)
            .Take(20)
            .ToList();
        var updated = settings with { RecentProjectPaths = paths };
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

using eBookEditor.Core.Services;

namespace eBookEditor.Tests.App;

internal class TestAppPaths : IAppPaths
{
    public TestAppPaths(string baseDirectory)
    {
        AppDataDirectory = baseDirectory;
        SettingsFilePath = Path.Combine(baseDirectory, "app-settings.json");
    }

    public string AppDataDirectory { get; }
    public string SettingsFilePath { get; }
}

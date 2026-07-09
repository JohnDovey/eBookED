namespace eBookEditor.Core.Services;

public interface IAppPaths
{
    string AppDataDirectory { get; }
    string SettingsFilePath { get; }
}

public class AppPaths : IAppPaths
{
    public string AppDataDirectory { get; }
    public string SettingsFilePath { get; }

    public AppPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AppDataDirectory = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Application Support", "eBookEditor")
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "eBookEditor")
                : Path.Combine(
                    Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                        ? xdg
                        : Path.Combine(home, ".config"),
                    "eBookEditor");

        SettingsFilePath = Path.Combine(AppDataDirectory, "app-settings.json");
    }
}

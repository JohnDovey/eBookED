namespace eBookEditor.Core.Services;

public interface IFontInstallDirectoryProvider
{
    /// <summary>The current user's per-user font directory, or null if none is known for this OS.</summary>
    string? UserFontsDirectory { get; }
}

public class FontInstallDirectoryProvider : IFontInstallDirectoryProvider
{
    public string? UserFontsDirectory { get; }

    public FontInstallDirectoryProvider()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        UserFontsDirectory = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Fonts")
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts")
                : OperatingSystem.IsLinux()
                    ? Path.Combine(
                        Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".local", "share"),
                        "fonts")
                    : null;
    }
}

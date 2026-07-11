using Avalonia.Platform.Storage;

namespace eBookEditor.App.Services;

/// <summary>
/// Opens a file picker rooted at the project's images/ folder (created if missing) and, if the
/// picked file isn't already inside it, copies it in (auto-renamed on a name collision) — the
/// same "images always end up self-contained in the project" behavior MainWindow's Insert Image
/// editor command already used, now shared so metadata forms (About the Author's photo, and
/// potentially cover/publisher-logo) can offer the same pick-instead-of-type-a-path UX instead
/// of a bare "path relative to project" text field.
/// </summary>
public static class ProjectImagePicker
{
    private static readonly FilePickerFileType ImageFileType = new("Images")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.svg", "*.webp"]
    };

    /// <summary>Returns the picked file's final name within imagesDir (after any
    /// dedup-renaming), or null if the user cancelled the picker.</summary>
    public static async Task<string?> PickAndCopyIntoImagesDirAsync(IStorageProvider storageProvider, string imagesDir, string dialogTitle)
    {
        Directory.CreateDirectory(imagesDir);

        var startLocation = await storageProvider.TryGetFolderFromPathAsync(imagesDir);
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = dialogTitle,
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = [ImageFileType]
        });

        var file = files.FirstOrDefault();
        if (file?.TryGetLocalPath() is not { } sourcePath)
            return null;

        var fileName = Path.GetFileName(sourcePath);
        var destinationPath = Path.Combine(imagesDir, fileName);

        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
        {
            destinationPath = UniqueDestinationPath(imagesDir, fileName);
            File.Copy(sourcePath, destinationPath);
            fileName = Path.GetFileName(destinationPath);
        }

        return fileName;
    }

    private static string UniqueDestinationPath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            return path;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{nameWithoutExtension} ({counter}){extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}

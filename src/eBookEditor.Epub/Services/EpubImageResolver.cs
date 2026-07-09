using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Rewrites project-relative markdown image references (which vary depending on which
/// subfolder — frontmatter/, chapters/, backmatter/ — the source file lives in) to a flat
/// "images/&lt;file&gt;" path matching the EPUB's OEBPS layout, and collects the resolved
/// source files that need to be copied into the package.
/// </summary>
internal static partial class EpubImageResolver
{
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex ImageRegex();

    public static string RewriteAndCollectImages(string markdown, string sourceDir, Dictionary<string, string> imagesToCopy)
    {
        return ImageRegex().Replace(markdown, match =>
        {
            var alt = match.Groups[1].Value;
            var path = match.Groups[2].Value;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return match.Value;

            var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, path));
            if (!File.Exists(absolutePath))
                return match.Value;

            if (!imagesToCopy.TryGetValue(absolutePath, out var destFileName))
            {
                destFileName = MakeUniqueFileName(absolutePath, imagesToCopy.Values);
                imagesToCopy[absolutePath] = destFileName;
            }

            return $"![{alt}](images/{destFileName})";
        });
    }

    public static string RegisterImage(string absolutePath, Dictionary<string, string> imagesToCopy)
    {
        if (imagesToCopy.TryGetValue(absolutePath, out var existing))
            return existing;

        var destFileName = MakeUniqueFileName(absolutePath, imagesToCopy.Values);
        imagesToCopy[absolutePath] = destFileName;
        return destFileName;
    }

    private static string MakeUniqueFileName(string absolutePath, IEnumerable<string> existingNames)
    {
        var fileName = Path.GetFileName(absolutePath);
        if (!existingNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            return fileName;

        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var hash = Math.Abs(absolutePath.GetHashCode()).ToString("x8");
        return $"{stem}-{hash}{ext}";
    }
}

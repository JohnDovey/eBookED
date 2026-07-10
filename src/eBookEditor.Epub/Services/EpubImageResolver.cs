using System.Net;
using System.Text.RegularExpressions;

namespace eBookEditor.Epub.Services;

/// <summary>
/// Rewrites project-relative HTML image references (which vary depending on which subfolder —
/// frontmatter/, chapters/, backmatter/ — the source file lives in) to a flat
/// "images/&lt;file&gt;" path matching the EPUB's OEBPS layout, and collects the resolved
/// source files that need to be copied into the package.
/// </summary>
internal static partial class EpubImageResolver
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex(@"src=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttributeRegex();

    public static string RewriteAndCollectImages(string html, string sourceDir, Dictionary<string, string> imagesToCopy)
    {
        return ImgTagRegex().Replace(html, imgMatch =>
        {
            var imgTag = imgMatch.Value;
            var srcMatch = SrcAttributeRegex().Match(imgTag);
            if (!srcMatch.Success)
                return imgTag;

            var path = WebUtility.HtmlDecode(srcMatch.Groups[1].Value);

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return imgTag;

            var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, path));
            if (!File.Exists(absolutePath))
                return imgTag;

            if (!imagesToCopy.TryGetValue(absolutePath, out var destFileName))
            {
                destFileName = MakeUniqueFileName(absolutePath, imagesToCopy.Values);
                imagesToCopy[absolutePath] = destFileName;
            }

            return SrcAttributeRegex().Replace(imgTag, $"src=\"images/{destFileName}\"", 1);
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

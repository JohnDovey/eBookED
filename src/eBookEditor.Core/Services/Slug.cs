namespace eBookEditor.Core.Services;

public static class Slug
{
    public static string Create(string text, string fallback = "item")
    {
        var lowered = text.Trim().ToLowerInvariant();
        var chars = lowered.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? fallback : slug;
    }
}

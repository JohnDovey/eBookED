using System.Globalization;

namespace eBookEditor.Html.Services;

/// <summary>
/// Turns a computed CSS property's raw text (as AngleSharp.Css 0.17.x returns it — the
/// declaration that won the cascade, not resolved to an absolute unit) into values usable for
/// rendering. Only the handful of shapes this app's own stylesheets actually use (em, %, pt,
/// px — see DefaultStylesheet.cs and "Vellum Serif.css") are supported; anything else returns
/// null rather than guessing.
/// </summary>
public static class CssValueParser
{
    /// <summary>Resolves a length or percentage against <paramref name="basePt"/> (e.g. the
    /// current font size in points), returning a concrete point value.</summary>
    public static float? ParseLength(string? value, float basePt)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();

        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase) &&
            TryParseFloat(value[..^2], out var em))
            return em * basePt;

        if (value.EndsWith('%') && TryParseFloat(value[..^1], out var percent))
            return percent / 100f * basePt;

        if (value.EndsWith("pt", StringComparison.OrdinalIgnoreCase) &&
            TryParseFloat(value[..^2], out var pt))
            return pt;

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
            TryParseFloat(value[..^2], out var px))
            return px * 0.75f; // 1px = 0.75pt at the standard 96dpi CSS reference resolution.

        return null;
    }

    /// <summary>True if a text-decoration-line-shaped value (possibly a "text-decoration"
    /// shorthand also carrying color/style) mentions <paramref name="token"/> (e.g.
    /// "underline", "line-through") as one of its space-separated keywords.</summary>
    public static bool HasKeyword(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(token, StringComparer.OrdinalIgnoreCase);

    private static bool TryParseFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

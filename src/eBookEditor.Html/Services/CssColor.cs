using System.Globalization;

namespace eBookEditor.Html.Services;

/// <summary>
/// Parses a CSS color value (hex, "rgb()"/"rgba()", or a named color) into a "#RRGGBB" hex
/// string, for renderers (QuestPDF, OpenXml) that take colors as hex rather than CSS text.
/// Only the named colors this app's own stylesheets actually use are recognized — see
/// DefaultStylesheet.cs's "mark { background-color: yellow }" baseline rule — plus a handful
/// of other common ones, rather than a full ~150-entry CSS named-color table.
/// </summary>
public static class CssColor
{
    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = "#FFFF00",
        ["red"] = "#FF0000",
        ["green"] = "#008000",
        ["blue"] = "#0000FF",
        ["black"] = "#000000",
        ["white"] = "#FFFFFF",
        ["gray"] = "#808080",
        ["grey"] = "#808080",
        ["orange"] = "#FFA500",
        ["pink"] = "#FFC0CB",
        ["purple"] = "#800080",
    };

    public static bool TryParseHex(string? value, out string hex)
    {
        hex = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        if (value is "transparent" or "rgba(0, 0, 0, 0)" or "initial" or "inherit" or "none")
            return false;

        if (value.StartsWith('#'))
        {
            hex = value;
            return true;
        }

        if (NamedColors.TryGetValue(value, out var named))
        {
            hex = named;
            return true;
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var start = value.IndexOf('(');
            var end = value.IndexOf(')');
            if (start < 0 || end < 0)
                return false;

            var parts = value[(start + 1)..end].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
                return false;

            if (!byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ||
                !byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ||
                !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                return false;

            hex = $"#{r:X2}{g:X2}{b:X2}";
            return true;
        }

        return false;
    }
}

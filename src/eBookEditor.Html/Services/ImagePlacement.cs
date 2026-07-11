namespace eBookEditor.Html.Services;

public enum ImageAlignment { Left, Center, Right }

/// <summary>An image's alignment and whether text should flow around it — Center never flows
/// (there's no sensible "flow to which side" for a centered block), so Flow is only ever true
/// alongside Left or Right.</summary>
public readonly record struct ImagePlacement(ImageAlignment Alignment, bool Flow)
{
    /// <summary>The <c>&lt;figure&gt;</c> wrapper's own inline style for this placement — driven
    /// by explicit attributes/styles rather than template-defined CSS classes, since a custom or
    /// foreign CSS template has no guarantee of defining alignment classes (this app's own
    /// ".drop-cap" float is already confirmed silently ignored by both the PDF and Word
    /// renderers, which don't read class-based float at all). Flow uses "float" (real text-wrap
    /// in a browser/EPUB context); no-flow uses "text-align" (the figure block itself moves, but
    /// nothing wraps around it).</summary>
    public string ToFigureStyle() =>
        Flow
            ? $"float:{(Alignment == ImageAlignment.Right ? "right" : "left")}"
            : $"text-align:{Alignment.ToString().ToLowerInvariant()}";
}

/// <summary>Parses a <c>&lt;figure&gt;</c> element's own "style" attribute (see
/// ImagePlacement.ToFigureStyle) back into an ImagePlacement — used by HtmlToPdfRenderer and
/// HtmlToDocxConverter, neither of which get real "float"/"text-align" support for free the way
/// a browser engine (Preview/WYSIWYG/EPUB) does, so both need to read this explicitly to size
/// and position an exported image correctly.</summary>
public static class ImagePlacementParser
{
    public static ImagePlacement Parse(string? figureStyle)
    {
        var style = figureStyle ?? "";

        if (Contains(style, "float", "left"))
            return new ImagePlacement(ImageAlignment.Left, Flow: true);
        if (Contains(style, "float", "right"))
            return new ImagePlacement(ImageAlignment.Right, Flow: true);
        if (Contains(style, "text-align", "right"))
            return new ImagePlacement(ImageAlignment.Right, Flow: false);
        if (Contains(style, "text-align", "left"))
            return new ImagePlacement(ImageAlignment.Left, Flow: false);

        return new ImagePlacement(ImageAlignment.Center, Flow: false);
    }

    private static bool Contains(string style, string property, string value) =>
        style.Contains($"{property}:{value}", StringComparison.OrdinalIgnoreCase) ||
        style.Contains($"{property}: {value}", StringComparison.OrdinalIgnoreCase);
}

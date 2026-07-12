using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using eBookEditor.Html.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DomElement = AngleSharp.Dom.IElement;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Renders a chapter/page's HTML body into a QuestPDF column — headings, paragraphs with
/// bold/italic/strikethrough/underline/highlight/subscript/superscript/link runs, bullet/
/// numbered lists (as prefixed paragraphs), tables, images (a whole-paragraph &lt;img&gt;, or
/// inside &lt;figure&gt;, resolved against <paramref name="sourceDir"/>), fenced code blocks
/// (&lt;pre&gt;), blockquotes and definition lists (recursed/indented), and any class the
/// selected CSS template defines — via AngleSharp.Css real cascade/specificity resolution
/// (HtmlStyleDocument), not a hardcoded class-name lookup, so EditorStyleCatalog classes and
/// arbitrary template CSS both actually affect rendering rather than surviving structurally
/// but unstyled. Not every CSS property QuestPDF simply has no primitive for (font-variant:
/// small-caps, letter-spacing, ::first-letter drop caps) — those are acknowledged, silent
/// no-ops rather than attempted approximations that would misrepresent the source styling.
///
/// Footnotes (see MainWindow.OnInsertFootnoteClick) render as a superscript reference number in
/// the text and a "Notes" section collected at the end of whichever chapter/page they were
/// written in (see RenderFootnotes) — matching this app's pre-HTML-refactor PDF footnote
/// behavior. True per-page-bottom footnotes, reflowing onto whichever physical page the
/// reference lands on, would need a custom pagination engine QuestPDF doesn't offer; this is the
/// same simplification the app has always made for PDF.
/// </summary>
internal class HtmlToPdfRenderer
{
    private const float DefaultFontSize = 11f;

    public void RenderHtmlBody(ColumnDescriptor column, string html, string? sourceDir, string? templateCss, string? sectionName = null, string? headingFontFamily = null)
    {
        // Registering the section on whichever content item happened to render first used to
        // miss entirely for chapters starting with a list/table/code block (those branches
        // below call column.Item() directly, bypassing the per-block section hookup) and
        // ALWAYS missed for a chapter with zero blocks (an unwritten "New Chapter" stub, still
        // empty) — in both cases the TOC's page-number lookup for that chapter had nothing to
        // resolve, rendering a literal "?", and the running header's "current chapter" tracking
        // got stuck on whatever chapter registered last. A zero-height marker item, emitted
        // unconditionally regardless of what (if anything) the chapter actually contains,
        // guarantees every spine item's section is registered exactly once.
        if (sectionName is not null)
            column.Item().Height(0).Section(sectionName).CaptureContentPosition(sectionName);

        var styled = HtmlStyleDocument.Parse(html, templateCss);
        var children = styled.Body.ChildNodes.ToList();

        // A left/right "flow" image (see ImagePlacement — Insert Image…'s "Flow text around
        // image" option) needs its own two-node lookahead: the image and the paragraph right
        // after it render together in one QuestPDF Row, approximating real text wrap (QuestPDF
        // has no float primitive of its own) — a best-effort approximation of a single adjacent
        // paragraph, not true multi-paragraph reflow, and only attempted at this top level, not
        // for an image nested inside another block container.
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is DomElement { TagName: "FIGURE" } figure
                && ImagePlacementParser.Parse(figure.GetAttribute("style")) is { Flow: true } placement
                && figure.QuerySelector("img") is { } flowImage
                && i + 1 < children.Count && children[i + 1] is DomElement { TagName: "P" } nextParagraph)
            {
                EmitDestinationSections(column, figure);
                RenderFlowedImageAndParagraph(column, flowImage, nextParagraph, placement, sourceDir, headingFontFamily, styled, DefaultFontSize);
                i++;
                continue;
            }

            RenderNode(column, children[i], sourceDir, headingFontFamily, styled, DefaultFontSize);
        }
    }

    private static void RenderFlowedImageAndParagraph(ColumnDescriptor column, DomElement image, DomElement paragraph, ImagePlacement placement, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        column.Item().PaddingBottom(8).Row(row =>
        {
            var imageWidth = ParseExplicitPixelSize(image, "width") ?? 150f;

            void RenderImageColumn() => RenderImage(row.ConstantItem(imageWidth), image, sourceDir, placement.Alignment, imageWidth);
            void RenderTextColumn() => row.RelativeItem().PaddingHorizontal(8).Text(text =>
                RenderInlineChildren(text, paragraph, baseFontSize, headingFontFamily, styles));

            if (placement.Alignment == ImageAlignment.Left)
            {
                RenderImageColumn();
                RenderTextColumn();
            }
            else
            {
                RenderTextColumn();
                RenderImageColumn();
            }
        });
    }

    private static void RenderNode(ColumnDescriptor column, INode node, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        if (node is not DomElement element)
            return;

        switch (element.TagName)
        {
            case "H1" or "H2" or "H3" or "H4" or "H5" or "H6":
                var level = element.TagName[1] - '0';
                var headingSize = level switch { 1 => 20f, 2 => 16f, _ => 13f };
                EmitDestinationSections(column, element);
                column.Item().PaddingTop(level == 1 ? 0 : 10).PaddingBottom(6).Text(text =>
                    RenderInlineChildren(text, element, headingSize, headingFontFamily, styles, forceBold: true));
                break;

            case "P" when TryGetSoleImage(element, out var soleImage):
                RenderImage(column.Item(), soleImage!, sourceDir);
                break;

            // A bare block-level <img> (e.g. a direct <figure> child, not wrapped in its own
            // <p>) needs the same handling as the "sole image in a paragraph" case above.
            case "IMG":
                RenderImage(column.Item(), element, sourceDir);
                break;

            case "DIV" when element.ClassList.Contains("footnotes"):
                RenderFootnotes(column, element, headingFontFamily, styles, baseFontSize);
                break;

            case "P" or "DIV":
                RenderBlockContainer(column, element, sourceDir, headingFontFamily, styles, baseFontSize);
                break;

            case "UL" or "OL":
                RenderList(column, element, sourceDir, headingFontFamily, styles, baseFontSize, ordered: element.TagName == "OL");
                break;

            case "TABLE":
                RenderTable(column, element, sourceDir, headingFontFamily, styles, baseFontSize);
                break;

            case "PRE":
                RenderCodeBlock(column, element);
                break;

            case "FIGURE":
                RenderFigure(column, element, sourceDir, headingFontFamily, styles, baseFontSize);
                break;

            case "FIGCAPTION":
                var captionStyle = styles.ComputedStyle(element);
                var captionFontSize = CssValueParser.ParseLength(captionStyle.GetPropertyValue("font-size"), baseFontSize) ?? baseFontSize;
                EmitDestinationSections(column, element);
                column.Item().PaddingBottom(8).Text(text =>
                    RenderInlineChildren(text, element, captionFontSize, headingFontFamily, styles));
                break;

            case "DL":
                RenderDefinitionList(column, element, styles, baseFontSize);
                break;

            case "BLOCKQUOTE":
                foreach (var child in element.Children)
                    RenderNode(column, child, sourceDir, headingFontFamily, styles, baseFontSize);
                break;
        }
    }

    private static void RenderBlockContainer(ColumnDescriptor column, DomElement element, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        var style = styles.ComputedStyle(element);
        var fontSize = CssValueParser.ParseLength(style.GetPropertyValue("font-size"), baseFontSize) ?? baseFontSize;

        // DIV is used both for EditorStyleCatalog's block styles (verse/inset/attribution/
        // drop-cap/caption — see DefaultStylesheet.cs) and, potentially, an arbitrary imported
        // HTML wrapper with no styling at all; either way, apply whatever computed padding/
        // alignment applies and recurse into any nested block children (a styled DIV wrapping a
        // <p>, the actual current shape EditorStyleCatalog block styles use) before falling
        // back to treating the DIV's own direct text as a paragraph.
        if (element.TagName == "DIV" && element.Children.Any(IsBlockElement))
        {
            IContainer container = column.Item();
            container = ApplyBlockStyle(container, style, baseFontSize);
            container.Column(inner =>
            {
                foreach (var child in element.Children)
                    RenderNode(inner, child, sourceDir, headingFontFamily, styles, fontSize);
            });
            return;
        }

        EmitDestinationSections(column, element);
        IContainer item = column.Item();
        item = ApplyBlockStyle(item, style, baseFontSize);
        item.PaddingBottom(8).Text(text => RenderInlineChildren(text, element, fontSize, headingFontFamily, styles));
    }

    /// <summary>
    /// Registers a zero-height QuestPDF Section (see PdfBuilder's own per-spine-item use of the
    /// same mechanism) for every "Mark Link Destination"/"Mark as Index Entry" marker (see
    /// InternalLinkConvention) found anywhere inside <paramref name="element"/> — block-level
    /// granularity, not the exact inline position, since QuestPDF's Section is a container-level
    /// primitive with no inline-text equivalent. A destination near the end of a long paragraph
    /// resolves to that paragraph's own page — accurate enough for "jump to roughly the right
    /// spot" purposes for a "dest:" link, and the documented "one linked page-number per marked
    /// occurrence, not fully deduplicated onto one physical page" simplification for the Index
    /// page's own per-occurrence "idx:" links (see PdfBuilder.RenderIndexPage).
    /// </summary>
    private static void EmitDestinationSections(ColumnDescriptor column, DomElement element)
    {
        // Checks element itself, not just its descendants — "dest:"/"idx:" markers are always
        // nested spans wrapping some marked text, but a "fig:" marker (see
        // InternalLinkConvention.FigureIdPrefix) sits directly on the <figure> element passed in
        // from RenderFigure/RenderFlowedImageAndParagraph, not on a descendant.
        if (InternalLinkConvention.IsInternalMarkerId(element.Id))
            column.Item().Height(0).Section(element.Id!);

        foreach (var marker in element.QuerySelectorAll("[id]").Where(e => InternalLinkConvention.IsInternalMarkerId(e.Id)))
            column.Item().Height(0).Section(marker.Id!);
    }

    private static bool IsBlockElement(DomElement element) => element.TagName is
        "P" or "DIV" or "UL" or "OL" or "TABLE" or "PRE" or "BLOCKQUOTE" or "DL" or "FIGURE" or
        "H1" or "H2" or "H3" or "H4" or "H5" or "H6";

    private static IContainer ApplyBlockStyle(IContainer container, ICssStyleDeclaration style, float baseFontSize)
    {
        var paddingLeft = CssValueParser.ParseLength(style.GetPropertyValue("padding-left"), baseFontSize);
        var paddingRight = CssValueParser.ParseLength(style.GetPropertyValue("padding-right"), baseFontSize);
        var marginTop = CssValueParser.ParseLength(style.GetPropertyValue("margin-top"), baseFontSize);

        if (paddingLeft is > 0) container = container.PaddingLeft(paddingLeft.Value);
        if (paddingRight is > 0) container = container.PaddingRight(paddingRight.Value);
        if (marginTop is > 0) container = container.PaddingTop(marginTop.Value);

        container = style.GetPropertyValue("text-align") switch
        {
            "right" => container.AlignRight(),
            "center" => container.AlignCenter(),
            _ => container,
        };

        return container;
    }

    private static void RenderList(ColumnDescriptor column, DomElement list, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize, bool ordered)
    {
        var index = 1;
        foreach (var item in list.Children.Where(c => c.TagName == "LI"))
        {
            var prefix = ordered ? $"{index}. " : "• ";
            EmitDestinationSections(column, item);
            column.Item().PaddingLeft(16).PaddingBottom(4).Text(text =>
            {
                text.Span(prefix);
                RenderInlineChildren(text, item, baseFontSize, headingFontFamily, styles);
            });
            index++;
        }
    }

    /// <summary>
    /// A "table.gallery" (see GalleryHtmlBuilder) renders each cell's &lt;figure&gt; as a real
    /// image via RenderFigure instead of the plain inline-text path every other table uses —
    /// borderless too, since a gallery is a layout grid, not tabular data. QuestPDF's Table
    /// component breaks rows cleanly across pages on its own, which is why galleries reuse this
    /// renderer rather than a Row-based grid (Row has no equivalent pagination).
    /// </summary>
    private static void RenderTable(ColumnDescriptor column, DomElement table, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        var rows = table.QuerySelectorAll("tr").ToList();
        if (rows.Count == 0)
            return;

        var columnCount = rows[0].Children.Count(c => c.TagName is "TD" or "TH");
        if (columnCount == 0)
            return;

        var isGallery = table.ClassList.Contains("gallery");

        column.Item().PaddingVertical(6).Table(questTable =>
        {
            questTable.ColumnsDefinition(columns =>
            {
                for (var i = 0; i < columnCount; i++)
                    columns.RelativeColumn();
            });

            uint rowIndex = 1;
            foreach (var row in rows)
            {
                uint colIndex = 1;
                foreach (var cell in row.Children.Where(c => c.TagName is "TD" or "TH"))
                {
                    if (isGallery)
                    {
                        var galleryCell = questTable.Cell().Row(rowIndex).Column(colIndex).Padding(6);
                        if (cell.QuerySelector("figure") is { } figure)
                            galleryCell.Column(cellColumn => RenderFigure(cellColumn, figure, sourceDir, headingFontFamily, styles, baseFontSize * 10 / 11));
                    }
                    else
                    {
                        var isHeader = cell.TagName == "TH";
                        var cellContainer = questTable.Cell().Row(rowIndex).Column(colIndex)
                            .Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                            .Background(isHeader ? Colors.Grey.Lighten3 : Colors.White);

                        cellContainer.Text(text => RenderInlineChildren(text, cell, baseFontSize * 10 / 11, null, styles, forceBold: isHeader));
                    }
                    colIndex++;
                }
                rowIndex++;
            }
        });
    }

    private static void RenderCodeBlock(ColumnDescriptor column, DomElement pre)
    {
        column.Item().PaddingVertical(6).Background(Colors.Grey.Lighten4).Padding(8)
            .Text(pre.TextContent.TrimEnd('\n')).FontFamily("Courier New").FontSize(9.5f);
    }

    private static void RenderDefinitionList(ColumnDescriptor column, DomElement dl, HtmlStyleDocument styles, float baseFontSize)
    {
        foreach (var child in dl.Children)
        {
            if (child.TagName == "DT")
            {
                EmitDestinationSections(column, child);
                column.Item().PaddingTop(6).Text(text =>
                    RenderInlineChildren(text, child, baseFontSize, null, styles, forceBold: true));
            }
            else if (child.TagName == "DD")
            {
                EmitDestinationSections(column, child);
                column.Item().PaddingLeft(16).PaddingBottom(2).Text(text =>
                    RenderInlineChildren(text, child, baseFontSize, null, styles));
            }
        }
    }

    /// <summary>
    /// Renders a "&lt;div class="footnotes"&gt;" block (see MainWindow.OnInsertFootnoteClick)
    /// as a horizontal rule, a "Notes" heading, and one "N. text" row per note — mirroring this
    /// app's pre-HTML-refactor PDF footnote layout. The note's own trailing back-link anchor
    /// ("&lt;a class='footnote-back-ref'&gt;") is skipped when rendering its text (see
    /// RenderInlineChildren's "A" case) — PDF has no in-document jump target for it to point at.
    /// </summary>
    private static void RenderFootnotes(ColumnDescriptor column, DomElement footnotesDiv, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        var items = footnotesDiv.QuerySelectorAll("li[id]")
            .Where(li => li.Id!.StartsWith("fn:", StringComparison.Ordinal))
            .ToList();
        if (items.Count == 0)
            return;

        column.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        var notesHeading = column.Item().PaddingTop(6).PaddingBottom(4).Text("Notes").Bold().FontSize(baseFontSize + 2);
        if (headingFontFamily is not null)
            notesHeading.FontFamily(headingFontFamily);

        foreach (var li in items)
        {
            var number = li.Id!["fn:".Length..];
            column.Item().PaddingBottom(4).Row(row =>
            {
                row.ConstantItem(18).Text($"{number}.").FontSize(baseFontSize * 0.85f);
                row.RelativeItem().Text(text =>
                    RenderInlineChildren(text, li.QuerySelector("p") ?? li, baseFontSize * 0.85f, headingFontFamily, styles));
            });
        }
    }

    private static bool TryGetSoleImage(DomElement paragraph, out DomElement? image)
    {
        var elements = paragraph.Children;
        if (elements.Length == 1 && elements[0].TagName == "IMG")
        {
            image = elements[0];
            return true;
        }

        image = null;
        return false;
    }

    /// <summary>The image itself, aligned per <paramref name="alignment"/> and sized to
    /// <paramref name="explicitWidthOverride"/> if given, else the image's own "width" attribute
    /// (see Insert Image…'s width/height fields) if present, else the previous fixed default —
    /// height is never read separately since QuestPDF's own FitWidth already preserves the
    /// original aspect ratio once width is fixed.</summary>
    private static void RenderImage(IContainer container, DomElement image, string? sourceDir, ImageAlignment alignment = ImageAlignment.Center, float? explicitWidthOverride = null)
    {
        var src = image.GetAttribute("src");
        if (sourceDir is null || string.IsNullOrWhiteSpace(src))
            return;

        var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, src));
        if (!File.Exists(absolutePath))
            return;

        var width = explicitWidthOverride ?? ParseExplicitPixelSize(image, "width") ?? 320f;
        var padded = container.PaddingBottom(8);
        var aligned = alignment switch
        {
            ImageAlignment.Left => padded.AlignLeft(),
            ImageAlignment.Right => padded.AlignRight(),
            _ => padded.AlignCenter(),
        };

        aligned.Element(el => el.MaxWidth(width).Image(absolutePath).FitWidth());
    }

    /// <summary>&lt;figure&gt; rendering for the non-flow case (flow — "float:left"/"right" —
    /// is instead handled by RenderHtmlBody's own top-level two-node lookahead, so it can pull
    /// in the following paragraph too; a flow figure reached through this path, e.g. nested
    /// inside another block container, still renders — just without the adjacent-paragraph
    /// approximation, a documented "top level only" scope reduction).</summary>
    private static void RenderFigure(ColumnDescriptor column, DomElement figure, string? sourceDir, string? headingFontFamily, HtmlStyleDocument styles, float baseFontSize)
    {
        EmitDestinationSections(column, figure);

        var img = figure.QuerySelector("img");
        if (img is null)
        {
            foreach (var child in figure.Children)
                RenderNode(column, child, sourceDir, headingFontFamily, styles, baseFontSize);
            return;
        }

        var placement = ImagePlacementParser.Parse(figure.GetAttribute("style"));
        RenderImage(column.Item(), img, sourceDir, placement.Alignment);

        if (figure.QuerySelector("figcaption") is { } figcaption)
        {
            var captionStyle = styles.ComputedStyle(figcaption);
            var captionFontSize = CssValueParser.ParseLength(captionStyle.GetPropertyValue("font-size"), baseFontSize) ?? baseFontSize;
            column.Item().PaddingBottom(8).Text(text => RenderInlineChildren(text, figcaption, captionFontSize, headingFontFamily, styles));
        }
    }

    private static float? ParseExplicitPixelSize(DomElement element, string attributeName) =>
        float.TryParse(element.GetAttribute(attributeName), out var value) && value > 0 ? value : null;

    private static void RenderInlineChildren(TextDescriptor text, DomElement parent, float baseFontSize, string? fallbackFontFamily, HtmlStyleDocument styles, bool forceBold = false)
    {
        var parentStyle = styles.ComputedStyle(parent);

        foreach (var child in parent.ChildNodes)
        {
            if (child is IText textNode)
            {
                if (textNode.Data.Length == 0)
                    continue;
                ApplyStyledSpan(text.Span(TransformText(textNode.Data, parentStyle)), parentStyle, baseFontSize, fallbackFontFamily, forceBold);
                continue;
            }

            if (child is not DomElement element)
                continue;

            switch (element.TagName)
            {
                case "BR":
                    text.EmptyLine();
                    break;

                // A footnote reference (see MainWindow.OnInsertFootnoteClick) — its number is
                // rendered directly rather than descending into its "<a>" child, since PDF has
                // no in-document jump target for that link to point at.
                case "SUP" when element.Id is { } supId && supId.StartsWith("fnref:", StringComparison.Ordinal):
                    text.Span(supId["fnref:".Length..]).FontSize(baseFontSize * 0.75f).Superscript();
                    break;

                // Word's own footnote UX already provides a way back to the reference; PDF's
                // "Notes" section (see RenderFootnotes) has no equivalent jump target for this
                // literal "↩" to point at, so it's dropped rather than shown as a dead link.
                case "A" when element.ClassList.Contains("footnote-back-ref"):
                    break;

                case "A":
                    var href = element.GetAttribute("href");
                    var linkStyle = styles.ComputedStyle(element);
                    var linkText = TransformText(element.TextContent, linkStyle);
                    if (string.IsNullOrWhiteSpace(href))
                        ApplyStyledSpan(text.Span(linkText), linkStyle, baseFontSize, fallbackFontFamily, forceBold);
                    else if (InternalLinkConvention.TryGetDestinationFragment(href, out var destinationId))
                        ApplyStyledSpan(text.SectionLink(linkText, destinationId), linkStyle, baseFontSize, fallbackFontFamily, forceBold);
                    else
                        ApplyStyledSpan(text.Hyperlink(linkText, href), linkStyle, baseFontSize, fallbackFontFamily, forceBold);
                    break;

                case "IMG":
                    // Inline (not whole-paragraph) images have no QuestPDF text-flow
                    // equivalent — silently skipped, matching this renderer's predecessor.
                    break;

                default:
                    var elementSize = CssValueParser.ParseLength(styles.ComputedStyle(element).GetPropertyValue("font-size"), baseFontSize) ?? baseFontSize;
                    RenderInlineChildren(text, element, elementSize, fallbackFontFamily, styles, forceBold);
                    break;
            }
        }
    }

    private static void ApplyStyledSpan(TextSpanDescriptor span, ICssStyleDeclaration style, float baseFontSize, string? fallbackFontFamily, bool forceBold)
    {
        var fontSize = CssValueParser.ParseLength(style.GetPropertyValue("font-size"), baseFontSize) ?? baseFontSize;
        span.FontSize(fontSize);

        var family = style.GetPropertyValue("font-family");
        if (!string.IsNullOrWhiteSpace(family))
            span.FontFamily(NormalizeFontFamily(family));
        else if (fallbackFontFamily is not null)
            span.FontFamily(fallbackFontFamily);

        var weight = style.GetPropertyValue("font-weight");
        if (forceBold || weight == "bold" || (int.TryParse(weight, out var numericWeight) && numericWeight >= 600))
            span.Bold();

        if (style.GetPropertyValue("font-style") is "italic" or "oblique")
            span.Italic();

        var decoration = $"{style.GetPropertyValue("text-decoration")} {style.GetPropertyValue("text-decoration-line")}";
        if (CssValueParser.HasKeyword(decoration, "underline"))
            span.Underline();
        if (CssValueParser.HasKeyword(decoration, "line-through"))
            span.Strikethrough();

        var backgroundColor = style.GetPropertyValue("background-color");
        if (CssColor.TryParseHex(backgroundColor, out var backgroundHex))
            span.BackgroundColor(Color.FromHex(backgroundHex));

        var verticalAlign = style.GetPropertyValue("vertical-align");
        if (verticalAlign == "sub") span.Subscript();
        if (verticalAlign == "super") span.Superscript();
    }

    // QuestPDF has no font-family fallback-list concept — pick the first name in a CSS
    // comma-separated list ("Georgia, serif" -> "Georgia") and let the font resolver at the
    // Skia/PDF layer fall back to whatever's actually installed if it isn't found.
    private static string NormalizeFontFamily(string cssFontFamily) =>
        cssFontFamily.Split(',')[0].Trim().Trim('"', '\'');

    // QuestPDF has no text-transform primitive — unlike Word's real w:caps run property
    // (see HtmlToDocxConverter), the only way to render text-transform: uppercase here is to
    // actually uppercase the string content itself before it's added as a span.
    private static string TransformText(string text, ICssStyleDeclaration style) =>
        style.GetPropertyValue("text-transform").Contains("uppercase", StringComparison.OrdinalIgnoreCase)
            ? text.ToUpperInvariant()
            : text;
}

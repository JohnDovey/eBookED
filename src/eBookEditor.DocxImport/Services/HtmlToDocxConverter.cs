using A = DocumentFormat.OpenXml.Drawing;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using eBookEditor.Html.Services;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DomElement = AngleSharp.Dom.IElement;

namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Converts a chapter's (or, fed HtmlBookAssembler.AssembleWholeBook's output, a whole book's)
/// HTML body into a .docx file — the reverse of DocxImportService. Handles headings (mapped to
/// Word's built-in Heading1-3 styles, so a round-tripped export re-imports with the same
/// chapter structure), paragraphs with bold/italic/strikethrough/underline/highlight/subscript/
/// superscript/link runs, bullet/numbered lists, tables, images (resolved against
/// <paramref name="sourceDir"/> in ConvertToFile), fenced code blocks (monospace, one Run per
/// line since Word Text elements don't respect embedded newlines), definition lists, and "hr"
/// thematic breaks as page breaks (matching how HtmlBookAssembler.AssembleWholeBook separates
/// front matter/chapters/back matter). List items are rendered as "•"/"1." prefixed paragraphs
/// rather than native Word list numbering, which needs a full NumberingDefinitionsPart — a
/// reasonable simplification since the visual result reads the same.
///
/// Any class the selected CSS template defines is resolved via AngleSharp.Css real cascade/
/// specificity (HtmlStyleDocument), not a hardcoded class-name lookup, so EditorStyleCatalog
/// classes and arbitrary template CSS both actually affect rendering. Word has real primitives
/// PDF/QuestPDF doesn't — true small caps (w:smallCaps), true letter spacing (w:spacing), and
/// true all-caps display (w:caps, which shows the text uppercase without changing the
/// underlying run text) — so those apply for real here rather than being approximated or
/// skipped.
///
/// Footnotes render as real Word footnotes (FootnotesPart/FootnoteReference, visible in Word's
/// own footnote pane) — see FootnoteContext/AppendFootnotesPart. A "&lt;sup id="fnref:N"&gt;"
/// reference (see MainWindow.OnInsertFootnoteClick) becomes a real FootnoteReference run instead
/// of literal superscript text, and its matching "&lt;div class="footnotes"&gt;" definition
/// block is skipped entirely from the main body — its content moves into the FootnotesPart
/// instead. The app's own footnote numbering restarts per chapter (see EditorStyleCatalog/
/// InsertFootnoteWindow's own doc comments), but Word's FootnotesPart requires globally-unique
/// ids across the whole document — assigning those by first-appearance order (not by the app's
/// own per-chapter display number) is what lets a whole-book export's several chapters each
/// restart at "1" in the source without an id collision; Word's own rendering numbers footnotes
/// by document flow order regardless of the id's numeric value, so the visible result is still
/// correctly sequential.
/// </summary>
public class HtmlToDocxConverter
{
    private const float DefaultFontSizePt = 11f;

    /// <summary>Keyed by the actual "&lt;sup&gt;" reference element (identity, not its "fnref:N"
    /// string — see BuildFootnoteContext for why the string alone isn't safely unique): that
    /// reference's matching &lt;li&gt; note content, and the globally-unique Word footnote id
    /// assigned to it (see the class doc comment).</summary>
    private sealed record FootnoteContext(Dictionary<DomElement, DomElement> Definitions, Dictionary<DomElement, uint> WordIds);

    public void ConvertToFile(string html, string title, string outputPath, string? sourceDir = null, string? templateCss = null)
    {
        var styled = HtmlStyleDocument.Parse(html, templateCss);
        var footnotes = BuildFootnoteContext(styled.Body);

        using var wordDocument = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(HeadingParagraph(title, "Title"));

        foreach (var node in styled.Body.ChildNodes)
            AppendNode(body, node, mainPart, sourceDir, styled, DefaultFontSizePt, footnotes);

        if (footnotes.WordIds.Count > 0)
            AppendFootnotesPart(mainPart, styled, DefaultFontSizePt, footnotes);

        mainPart.Document.Save();
    }

    /// <summary>
    /// Word ids are assigned in first-appearance order of each "&lt;sup id="fnref:N"&gt;"
    /// reference. The "fn:N"/"fnref:N" strings alone aren't safely unique keys for a whole-book
    /// export: HtmlBookAssembler concatenates every chapter into one HTML string, and this
    /// app's own footnote numbering restarts at 1 in each chapter, so a naive "fn:1" lookup
    /// would collide across chapters. Instead this walks the body's top-level children in
    /// document order, treating a "&lt;div class="footnotes"&gt;" as closing out whichever
    /// preceding references are still unresolved — matching each of its &lt;li&gt; entries to
    /// the pending reference with the same "N" (they always belong to the same chapter, since
    /// HtmlBookAssembler's own "&lt;hr&gt;" section separators and this app's single-chapter
    /// files both keep a chapter's references and its own footnotes block together with no
    /// other chapter's content between them) — keyed by the reference element itself, not the
    /// string, so identically-numbered notes from different chapters never collide.
    /// </summary>
    private static FootnoteContext BuildFootnoteContext(DomElement body)
    {
        var definitions = new Dictionary<DomElement, DomElement>();
        var wordIds = new Dictionary<DomElement, uint>();
        uint nextId = 1;
        var pendingReferences = new List<DomElement>();

        foreach (var child in body.Children)
        {
            if (child.TagName == "DIV" && child.ClassList.Contains("footnotes"))
            {
                foreach (var li in child.QuerySelectorAll("li[id]").Where(li => li.Id!.StartsWith("fn:", StringComparison.Ordinal)))
                {
                    var number = li.Id!["fn:".Length..];
                    var reference = pendingReferences.FirstOrDefault(sup => sup.Id == "fnref:" + number);
                    if (reference is null)
                        continue;

                    definitions[reference] = li;
                    wordIds[reference] = nextId++;
                    pendingReferences.Remove(reference);
                }

                // Any left unmatched here are dangling references with no definition — leave
                // them out of wordIds; AppendInlineChildren's SUP case falls through to the
                // default (generic) handling for those rather than emitting a broken reference.
                pendingReferences.Clear();
                continue;
            }

            pendingReferences.AddRange(child.QuerySelectorAll("sup[id]").Where(sup => sup.Id!.StartsWith("fnref:", StringComparison.Ordinal)));
        }

        return new FootnoteContext(definitions, wordIds);
    }

    private static void AppendNode(Body body, INode node, MainDocumentPart mainPart, string? sourceDir, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes)
    {
        if (node is not DomElement element)
            return;

        switch (element.TagName)
        {
            case "H1" or "H2" or "H3" or "H4" or "H5" or "H6":
                var level = element.TagName[1] - '0';
                body.Append(HeadingParagraph(element.TextContent, $"Heading{Math.Clamp(level, 1, 3)}"));
                break;

            case "P" when TryGetSoleImage(element, out var soleImage):
                AppendImage(body, soleImage!, mainPart, sourceDir);
                break;

            // A bare block-level <img> (e.g. a direct <figure> child, not wrapped in its own
            // <p>) needs the same handling as the "sole image in a paragraph" case above.
            case "IMG":
                AppendImage(body, element, mainPart, sourceDir);
                break;

            // Its content already moved into the FootnotesPart (see ConvertToFile/
            // AppendFootnotesPart) — rendering it again here would duplicate every note inline
            // in the body as well as in Word's real footnote pane.
            case "DIV" when element.ClassList.Contains("footnotes"):
                break;

            case "P" or "DIV":
                AppendBlockContainer(body, element, mainPart, sourceDir, styles, baseFontSizePt, footnotes);
                break;

            case "UL" or "OL":
                AppendList(body, element, mainPart, styles, baseFontSizePt, footnotes, ordered: element.TagName == "OL");
                break;

            case "TABLE":
                body.Append(BuildTable(element, mainPart, styles, baseFontSizePt, footnotes));
                break;

            case "PRE":
                body.Append(BuildCodeParagraph(element));
                break;

            case "FIGURE":
                foreach (var child in element.Children)
                    AppendNode(body, child, mainPart, sourceDir, styles, baseFontSizePt, footnotes);
                break;

            case "FIGCAPTION":
                var captionStyle = styles.ComputedStyle(element);
                var captionSize = CssValueParser.ParseLength(captionStyle.GetPropertyValue("font-size"), baseFontSizePt) ?? baseFontSizePt;
                var captionParagraph = new Paragraph();
                AppendInlineChildren(captionParagraph, element, mainPart, styles, captionSize, footnotes);
                body.Append(captionParagraph);
                break;

            case "DL":
                AppendDefinitionList(body, element, mainPart, styles, baseFontSizePt, footnotes);
                break;

            case "BLOCKQUOTE":
                foreach (var child in element.Children)
                    AppendNode(body, child, mainPart, sourceDir, styles, baseFontSizePt, footnotes);
                break;

            // HtmlBookAssembler.AssembleWholeBook separates front matter/chapters/back matter
            // with an "hr" thematic break; rendering it as a page break here gives a whole-book
            // Word export the same "every section starts on a new page" convention already used
            // by the EPUB and PDF exports.
            case "HR":
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                break;
        }
    }

    private static void AppendBlockContainer(Body body, DomElement element, MainDocumentPart mainPart, string? sourceDir, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes)
    {
        var style = styles.ComputedStyle(element);
        var fontSize = CssValueParser.ParseLength(style.GetPropertyValue("font-size"), baseFontSizePt) ?? baseFontSizePt;

        // DIV is used both for EditorStyleCatalog's block styles (verse/inset/attribution/
        // drop-cap/caption — see DefaultStylesheet.cs) and, potentially, an arbitrary imported
        // HTML wrapper with no styling at all; either way, recurse into any nested block
        // children (a styled DIV wrapping a <p>, the actual current shape EditorStyleCatalog
        // block styles use), passing this DIV's own resolved font size/indentation down so
        // nested paragraphs inherit it, rather than treating the DIV's own direct text as a
        // paragraph.
        if (element.TagName == "DIV" && element.Children.Any(IsBlockElement))
        {
            foreach (var child in element.Children)
                AppendNode(body, child, mainPart, sourceDir, styles, fontSize, footnotes);
            return;
        }

        var paragraph = new Paragraph();
        ApplyParagraphBlockStyle(paragraph, style, baseFontSizePt);
        AppendInlineChildren(paragraph, element, mainPart, styles, fontSize, footnotes);
        body.Append(paragraph);
    }

    private static bool IsBlockElement(DomElement element) => element.TagName is
        "P" or "DIV" or "UL" or "OL" or "TABLE" or "PRE" or "BLOCKQUOTE" or "DL" or "FIGURE" or
        "H1" or "H2" or "H3" or "H4" or "H5" or "H6";

    private static void ApplyParagraphBlockStyle(Paragraph paragraph, ICssStyleDeclaration style, float baseFontSizePt)
    {
        var properties = new ParagraphProperties();

        var paddingLeftPt = CssValueParser.ParseLength(style.GetPropertyValue("padding-left"), baseFontSizePt);
        if (paddingLeftPt is > 0)
            properties.Append(new Indentation { Left = PointsToTwips(paddingLeftPt.Value).ToString() });

        var justification = style.GetPropertyValue("text-align") switch
        {
            "right" => JustificationValues.Right,
            "center" => JustificationValues.Center,
            "justify" => JustificationValues.Both,
            _ => (JustificationValues?)null,
        };
        if (justification is not null)
            properties.Append(new Justification { Val = justification });

        if (properties.HasChildren)
            paragraph.Append(properties);
    }

    private static void AppendList(Body body, DomElement list, MainDocumentPart mainPart, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes, bool ordered)
    {
        var index = 1;
        foreach (var item in list.Children.Where(c => c.TagName == "LI"))
        {
            var paragraph = new Paragraph();
            paragraph.Append(new Run(new Text(ordered ? $"{index}. " : "• ") { Space = SpaceProcessingModeValues.Preserve }));
            AppendInlineChildren(paragraph, item, mainPart, styles, baseFontSizePt, footnotes);
            body.Append(paragraph);
            index++;
        }
    }

    private static void AppendDefinitionList(Body body, DomElement dl, MainDocumentPart mainPart, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes)
    {
        foreach (var child in dl.Children)
        {
            if (child.TagName == "DT")
            {
                var paragraph = new Paragraph();
                AppendInlineChildren(paragraph, child, mainPart, styles, baseFontSizePt, footnotes, forceBold: true);
                body.Append(paragraph);
            }
            else if (child.TagName == "DD")
            {
                var paragraph = new Paragraph(new ParagraphProperties(new Indentation { Left = "720" }));
                AppendInlineChildren(paragraph, child, mainPart, styles, baseFontSizePt, footnotes);
                body.Append(paragraph);
            }
        }
    }

    private static bool TryGetSoleImage(DomElement paragraph, out DomElement? image)
    {
        var children = paragraph.Children;
        if (children.Length == 1 && children[0].TagName == "IMG")
        {
            image = children[0];
            return true;
        }

        image = null;
        return false;
    }

    private static Paragraph HeadingParagraph(string text, string styleId) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), new Run(new Text(text)));

    private static Paragraph BuildCodeParagraph(DomElement pre)
    {
        var lines = pre.TextContent.TrimEnd('\n').Split('\n');
        var paragraph = new Paragraph();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                paragraph.Append(new Run(new Break()));

            paragraph.Append(new Run(
                new RunProperties(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" }),
                new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
        }
        return paragraph;
    }

    private static DocumentFormat.OpenXml.Wordprocessing.Table BuildTable(DomElement table, MainDocumentPart mainPart, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes)
    {
        var docxTable = new DocumentFormat.OpenXml.Wordprocessing.Table();
        docxTable.AppendChild(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        var rows = table.QuerySelectorAll("tr").ToList();
        var columnCount = rows.Count > 0 ? rows[0].Children.Count(c => c.TagName is "TD" or "TH") : 0;
        var tableGrid = new TableGrid();
        for (var i = 0; i < columnCount; i++)
            tableGrid.Append(new GridColumn());
        docxTable.AppendChild(tableGrid);

        foreach (var row in rows)
        {
            var docxRow = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
            foreach (var cell in row.Children.Where(c => c.TagName is "TD" or "TH"))
            {
                var paragraph = new Paragraph();
                AppendInlineChildren(paragraph, cell, mainPart, styles, baseFontSizePt, footnotes, forceBold: cell.TagName == "TH");
                if (!paragraph.HasChildren)
                    paragraph.Append(new Run(new Text(string.Empty)));

                docxRow.Append(new DocumentFormat.OpenXml.Wordprocessing.TableCell(paragraph));
            }
            docxTable.Append(docxRow);
        }

        return docxTable;
    }

    private static void AppendImage(Body body, DomElement image, MainDocumentPart mainPart, string? sourceDir)
    {
        var src = image.GetAttribute("src");
        if (sourceDir is null || string.IsNullOrWhiteSpace(src))
            return;

        var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, src));
        if (!File.Exists(absolutePath))
            return;

        var partType = ImagePartTypeFor(absolutePath);
        if (partType is null)
            return;

        var imagePart = mainPart.AddImagePart(partType.Value);
        using (var stream = File.OpenRead(absolutePath))
            imagePart.FeedData(stream);
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var (widthPx, heightPx) = ImageDimensionReader.TryGetDimensions(absolutePath) ?? (400, 300);
        const long maxWidthEmu = 5486400L; // 6 inches
        var widthEmu = (long)widthPx * 9525L;
        var heightEmu = (long)heightPx * 9525L;
        if (widthEmu > maxWidthEmu)
        {
            var scale = (double)maxWidthEmu / widthEmu;
            heightEmu = (long)(heightEmu * scale);
            widthEmu = maxWidthEmu;
        }

        // wp:docPr/@id and pic:cNvPr/@id must be unique within the whole document —
        // OpenXmlValidator flags a duplicate as a schema error, and Word won't reliably open
        // the file. A hardcoded "1" for every image (the bug this replaces) only worked by
        // accident for documents with at most one image; a real multi-chapter book with more
        // than one caught it immediately. Counting the Drawing elements already in the
        // document (mainPart is already threaded through the whole call chain, so this needs
        // no new state or signature changes) gives a value that's always unique so far.
        var drawingId = (uint)mainPart.Document!.Descendants<Drawing>().Count() + 1;
        body.Append(new Paragraph(new Run(BuildImageDrawing(relationshipId, drawingId, widthEmu, heightEmu))));
    }

    private static PartTypeInfo? ImagePartTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ImagePartType.Png,
        ".jpg" or ".jpeg" => ImagePartType.Jpeg,
        ".gif" => ImagePartType.Gif,
        ".bmp" => ImagePartType.Bmp,
        _ => null,
    };

    private static Drawing BuildImageDrawing(string relationshipId, uint drawingId, long cx, long cy) => new(
        new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = drawingId, Name = "Picture" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = drawingId, Name = "Picture" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })
                    )
                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
        )
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U,
        });

    private static void AppendInlineChildren(Paragraph paragraph, DomElement parent, MainDocumentPart mainPart, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes, bool forceBold = false)
    {
        var parentStyle = styles.ComputedStyle(parent);

        foreach (var child in parent.ChildNodes)
        {
            if (child is IText textNode)
            {
                if (textNode.Data.Length == 0)
                    continue;
                paragraph.Append(RunFor(textNode.Data, parentStyle, baseFontSizePt, forceBold));
                continue;
            }

            if (child is not DomElement element)
                continue;

            switch (element.TagName)
            {
                case "BR":
                    paragraph.Append(new Run(new Break()));
                    break;

                // Word's own footnote UX already provides a way back to the reference —
                // rendering this literal "↩" (with a dead link, since Word has no equivalent
                // fragment-anchor target for it) would just be redundant/confusing.
                case "A" when element.ClassList.Contains("footnote-back-ref"):
                    break;

                // A footnote reference (see MainWindow.OnInsertFootnoteClick) becomes a real
                // Word FootnoteReference run instead of literal "<sup><a>N</a></sup>" text —
                // its content lives in the FootnotesPart (see AppendFootnotesPart), addressed
                // by the id BuildFootnoteContext assigned it.
                case "SUP" when footnotes.WordIds.TryGetValue(element, out var wordId):
                    paragraph.Append(new Run(
                        new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }),
                        new FootnoteReference { Id = wordId }));
                    break;

                case "A":
                    var href = element.GetAttribute("href");
                    var linkStyle = styles.ComputedStyle(element);
                    var linkRun = RunFor(element.TextContent, linkStyle, baseFontSizePt, forceBold);
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        paragraph.Append(linkRun);
                    }
                    else if (InternalLinkConvention.TryGetDestinationFragment(href, out var linkDestinationId))
                    {
                        paragraph.Append(new Hyperlink(linkRun) { Anchor = BookmarkNameFor(linkDestinationId) });
                    }
                    else
                    {
                        var relationshipId = mainPart.AddHyperlinkRelationship(SafeHyperlinkUri(href), true).Id;
                        paragraph.Append(new Hyperlink(linkRun) { Id = relationshipId });
                    }
                    break;

                // A "Mark Link Destination" marker (see InternalLinkConvention) becomes a real
                // Word bookmark — w:bookmarkStart/w:bookmarkEnd bracketing the same run(s) its
                // marked text renders as — so a matching internal hyperlink's Anchor (above) has
                // a real, Word-native jump target rather than a dead link.
                case "SPAN" when element.Id is { } destinationId && destinationId.StartsWith(InternalLinkConvention.DestinationIdPrefix, StringComparison.Ordinal):
                    var bookmarkId = (mainPart.Document!.Descendants<BookmarkStart>().Count() + 1).ToString();
                    var bookmarkName = BookmarkNameFor(destinationId);
                    paragraph.Append(new BookmarkStart { Id = bookmarkId, Name = bookmarkName });
                    AppendInlineChildren(paragraph, element, mainPart, styles, baseFontSizePt, footnotes, forceBold);
                    paragraph.Append(new BookmarkEnd { Id = bookmarkId });
                    break;

                case "IMG":
                    // Inline (not whole-paragraph) images have no simple Word text-flow
                    // equivalent — silently skipped, matching this converter's predecessor.
                    break;

                default:
                    AppendInlineChildren(paragraph, element, mainPart, styles, baseFontSizePt, footnotes, forceBold);
                    break;
            }
        }
    }

    /// <summary>
    /// Builds the FootnotesPart every id in footnotes.WordIds needs — a required separator and
    /// continuation-separator entry (Word's own schema expects these two reserved ids, -1 and
    /// 0, even though this app never triggers the "continued on next page" case they exist
    /// for), then one real w:footnote per note, in Word-id order so they read in the same
    /// sequence as their references appear in the body.
    /// </summary>
    private static void AppendFootnotesPart(MainDocumentPart mainPart, HtmlStyleDocument styles, float baseFontSizePt, FootnoteContext footnotes)
    {
        var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
        var footnotesRoot = new Footnotes(
            new DocumentFormat.OpenXml.Wordprocessing.Footnote(new Paragraph(new Run(new SeparatorMark())))
            {
                Type = FootnoteEndnoteValues.Separator,
                Id = -1,
            },
            new DocumentFormat.OpenXml.Wordprocessing.Footnote(new Paragraph(new Run(new ContinuationSeparatorMark())))
            {
                Type = FootnoteEndnoteValues.ContinuationSeparator,
                Id = 0,
            });

        foreach (var (reference, wordId) in footnotes.WordIds.OrderBy(entry => entry.Value))
        {
            if (!footnotes.Definitions.TryGetValue(reference, out var li))
                continue;

            var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "FootnoteText" }));
            paragraph.Append(new Run(new RunProperties(new RunStyle { Val = "FootnoteText" }), new FootnoteReferenceMark()));
            paragraph.Append(new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve }));

            // The note's own text lives inside a <p> (see MainWindow.InsertOrAppendFootnoteDefinition's
            // "<li id='fn:N'><p>{note} <a class='footnote-back-ref'>...</a></p></li>" shape) —
            // fall back to the <li> itself defensively, in case a hand-edited chapter omits it.
            var contentElement = li.QuerySelector("p") ?? li;
            AppendInlineChildren(paragraph, contentElement, mainPart, styles, baseFontSizePt, footnotes);

            footnotesRoot.Append(new DocumentFormat.OpenXml.Wordprocessing.Footnote(paragraph) { Id = wordId });
        }

        footnotesPart.Footnotes = footnotesRoot;
    }

    /// <summary>
    /// Chapter/front/back-matter links (e.g. TOC page links to other spine items) can contain
    /// characters RFC 3986 doesn't allow unescaped in a URI. Feeding that straight into
    /// AddHyperlinkRelationship produces a package the OpenXml SDK itself flags internally as
    /// containing a "malformed URI relationship" — in practice this wrote a literal
    /// "rewritten://&lt;guid&gt;" placeholder into the saved .docx's real relationship target
    /// instead of ever resolving back to the real path, which is exactly the kind of dangling/
    /// invalid relationship Word's stricter parser refuses to open at all ("isn't in the
    /// correct format"), not a cosmetic glitch. Percent-encoding each path segment (not the
    /// whole string, so "/" separators survive) keeps the destination well-formed without
    /// needing to know whether it's a local relative path or a real absolute URL.
    /// </summary>
    private static Uri SafeHyperlinkUri(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return new Uri(string.Empty, UriKind.Relative);

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return absolute;

        var encoded = string.Join('/', url.Split('/').Select(Uri.EscapeDataString));
        return new Uri(encoded, UriKind.RelativeOrAbsolute);
    }

    /// <summary>A Word bookmark name is restricted to letters/digits/underscore (no colons or
    /// hyphens) — this app's own "dest:{slug}" ids use both, so every non-alphanumeric character
    /// is mapped to '_'. Deterministic from the destination id alone, so a span's own
    /// w:bookmarkStart/End Name and any &lt;a href="...#dest:{slug}"&gt; pointing at it always
    /// derive the identical Word bookmark name independently, with no lookup table needed.</summary>
    private static string BookmarkNameFor(string destinationId) =>
        string.Concat(destinationId.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

    private static Run RunFor(string text, ICssStyleDeclaration style, float baseFontSizePt, bool forceBold)
    {
        var fontSizePt = CssValueParser.ParseLength(style.GetPropertyValue("font-size"), baseFontSizePt) ?? baseFontSizePt;
        var weight = style.GetPropertyValue("font-weight");
        var bold = forceBold || weight == "bold" || (int.TryParse(weight, out var numericWeight) && numericWeight >= 600);
        var italic = style.GetPropertyValue("font-style") is "italic" or "oblique";
        var decoration = $"{style.GetPropertyValue("text-decoration")} {style.GetPropertyValue("text-decoration-line")}";
        var strikethrough = CssValueParser.HasKeyword(decoration, "line-through");
        var underline = CssValueParser.HasKeyword(decoration, "underline");
        var smallCaps = style.GetPropertyValue("font-variant").Contains("small-caps", StringComparison.OrdinalIgnoreCase);
        var uppercase = style.GetPropertyValue("text-transform").Contains("uppercase", StringComparison.OrdinalIgnoreCase);
        var verticalAlign = style.GetPropertyValue("vertical-align");
        var subscript = verticalAlign == "sub";
        var superscript = verticalAlign == "super";
        var fontFamily = style.GetPropertyValue("font-family");
        var letterSpacingPt = CssValueParser.ParseLength(style.GetPropertyValue("letter-spacing"), fontSizePt);
        var hasHighlight = CssColor.TryParseHex(style.GetPropertyValue("background-color"), out _);

        var run = new Run();
        var properties = new RunProperties();
        // CT_RPr child order is schema-fixed (rFonts, b, i, caps, smallCaps, strike, color,
        // spacing, sz, highlight, u, ..., vertAlign) — Word refuses to open a document with
        // elements out of this order, so append in that sequence, not the order features
        // happen to be computed in above.
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            var family = fontFamily.Split(',')[0].Trim().Trim('"', '\'');
            properties.Append(new RunFonts { Ascii = family, HighAnsi = family });
        }
        if (bold) properties.Append(new Bold());
        if (italic) properties.Append(new Italic());
        if (uppercase) properties.Append(new Caps());
        if (smallCaps) properties.Append(new SmallCaps());
        if (strikethrough) properties.Append(new Strike());
        if (letterSpacingPt is not null) properties.Append(new Spacing { Val = PointsToTwips(letterSpacingPt.Value) });
        properties.Append(new FontSize { Val = (fontSizePt * 2).ToString("0") });
        if (hasHighlight) properties.Append(new Highlight { Val = HighlightColorValues.Yellow });
        if (underline) properties.Append(new Underline { Val = UnderlineValues.Single });
        if (subscript) properties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });
        if (superscript) properties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });

        run.Append(properties);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static int PointsToTwips(float points) => (int)Math.Round(points * 20);
}

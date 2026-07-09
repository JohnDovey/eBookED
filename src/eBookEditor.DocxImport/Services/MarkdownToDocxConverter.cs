using System.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using eBookEditor.Markdown.Services;
using MdFootnote = Markdig.Extensions.Footnotes.Footnote;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;

namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Converts a chapter's (or, fed MarkdownExportService.ExportWholeBook's output, a whole
/// book's) Markdown into a .docx file — the reverse of DocxImportService. Handles headings
/// (mapped to Word's built-in Heading1-3 styles, so a round-tripped export re-imports with
/// the same chapter structure), paragraphs with bold/italic/strikethrough/highlight/
/// subscript/superscript/link runs, task lists, definition lists, bullet/numbered lists,
/// tables, images (resolved against <paramref name="sourceDir"/> in ConvertToFile), fenced/
/// indented code blocks (monospace, one Run per line since Word Text elements don't respect
/// embedded newlines), custom containers (":::" fenced divs — rendered by recursing into
/// their content; the CSS class they carry is EPUB-only, Word has no stylesheet to consult),
/// footnotes as real Word footnotes (FootnotesPart/FootnoteReference), and "---" thematic
/// breaks as page breaks (matching how ExportWholeBook separates front matter/chapters/back
/// matter). List items are rendered as "•"/"1." prefixed paragraphs rather than native Word
/// list numbering, which needs a full NumberingDefinitionsPart — a reasonable simplification
/// since the visual result reads the same.
/// </summary>
public class MarkdownToDocxConverter
{
    public void ConvertToFile(string markdown, string title, string outputPath, string? sourceDir = null)
    {
        var document = Markdig.Markdown.Parse(markdown, MarkdownPipelineFactory.Create());

        using var wordDocument = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(HeadingParagraph(title, "Title"));

        foreach (var block in document)
            AppendBlock(body, block, mainPart, sourceDir);

        mainPart.Document.Save();
    }

    private static void AppendBlock(Body body, Block block, MainDocumentPart mainPart, string? sourceDir)
    {
        switch (block)
        {
            case HeadingBlock heading:
                body.Append(HeadingParagraph(PlainText(heading.Inline), $"Heading{Math.Clamp(heading.Level, 1, 3)}"));
                break;

            case ParagraphBlock { Inline: not null } paragraph when TryGetSoleImage(paragraph.Inline, out var imageLink):
                AppendImage(body, imageLink!, mainPart, sourceDir);
                break;

            case ParagraphBlock { Inline: not null } paragraph:
                body.Append(TextParagraph(paragraph.Inline, mainPart));
                break;

            case ListBlock list:
                var index = 1;
                foreach (var listItem in list.OfType<ListItemBlock>())
                {
                    var prefix = list.IsOrdered ? $"{index}. " : "• ";
                    foreach (var itemBlock in listItem.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
                        body.Append(TextParagraph(itemBlock.Inline!, mainPart, prefix));
                    index++;
                }
                break;

            case MdTable table:
                body.Append(BuildTable(table, mainPart));
                break;

            case CodeBlock codeBlock:
                body.Append(BuildCodeParagraph(codeBlock));
                break;

            case FootnoteGroup group:
                AppendFootnotesPart(mainPart, group);
                break;

            case DefinitionList definitionList:
                AppendDefinitionList(body, definitionList, mainPart);
                break;

            case CustomContainer container:
                foreach (var child in container)
                    AppendBlock(body, child, mainPart, sourceDir);
                break;

            // MarkdownExportService.ExportWholeBook separates front matter/chapters/back
            // matter with a "---" thematic break; rendering it as a page break here gives a
            // whole-book Word export the same "every section starts on a new page"
            // convention already used by the EPUB and PDF exports.
            case ThematicBreakBlock:
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    AppendBlock(body, child, mainPart, sourceDir);
                break;
        }
    }

    private static void AppendDefinitionList(Body body, DefinitionList definitionList, MainDocumentPart mainPart)
    {
        foreach (var item in definitionList.OfType<DefinitionItem>())
        {
            foreach (var child in item)
            {
                if (child is DefinitionTerm term)
                {
                    var paragraph = new Paragraph();
                    AppendInlines(paragraph, term.Inline, mainPart, bold: true, italic: false);
                    body.Append(paragraph);
                }
                else if (child is ParagraphBlock { Inline: not null } definition)
                {
                    var paragraph = new Paragraph(new ParagraphProperties(new Indentation { Left = "720" }));
                    AppendInlines(paragraph, definition.Inline, mainPart, bold: false, italic: false);
                    body.Append(paragraph);
                }
            }
        }
    }

    private static bool TryGetSoleImage(ContainerInline container, out LinkInline? image)
    {
        Inline? only = container.FirstChild;
        if (only is LinkInline { IsImage: true } link && only.NextSibling is null)
        {
            image = link;
            return true;
        }

        image = null;
        return false;
    }

    private static Paragraph HeadingParagraph(string text, string styleId) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), new Run(new Text(text)));

    private static Paragraph TextParagraph(ContainerInline inline, MainDocumentPart mainPart, string prefix = "")
    {
        var paragraph = new Paragraph();
        if (prefix.Length > 0)
            paragraph.Append(new Run(new Text(prefix) { Space = SpaceProcessingModeValues.Preserve }));

        AppendInlines(paragraph, inline, mainPart, bold: false, italic: false);
        return paragraph;
    }

    private static Paragraph BuildCodeParagraph(CodeBlock codeBlock)
    {
        var paragraph = new Paragraph();
        for (var i = 0; i < codeBlock.Lines.Count; i++)
        {
            if (i > 0)
                paragraph.Append(new Run(new Break()));

            var line = codeBlock.Lines.Lines[i].Slice.ToString();
            paragraph.Append(new Run(
                new RunProperties(new RunFonts { Ascii = "Courier New", HighAnsi = "Courier New" }),
                new Text(line) { Space = SpaceProcessingModeValues.Preserve }));
        }
        return paragraph;
    }

    private static Table BuildTable(MdTable table, MainDocumentPart mainPart)
    {
        var docxTable = new Table();
        docxTable.AppendChild(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        foreach (var row in table.OfType<MdTableRow>())
        {
            var docxRow = new TableRow();
            foreach (var cell in row.OfType<MdTableCell>())
            {
                var paragraph = new Paragraph();
                foreach (var cellBlock in cell.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
                    AppendInlines(paragraph, cellBlock.Inline!, mainPart, bold: row.IsHeader, italic: false);
                if (!paragraph.HasChildren)
                    paragraph.Append(new Run(new Text(string.Empty)));

                docxRow.Append(new TableCell(paragraph));
            }
            docxTable.Append(docxRow);
        }

        return docxTable;
    }

    private static void AppendImage(Body body, LinkInline imageLink, MainDocumentPart mainPart, string? sourceDir)
    {
        if (sourceDir is null || string.IsNullOrWhiteSpace(imageLink.Url))
            return;

        var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, imageLink.Url));
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

        body.Append(new Paragraph(new Run(BuildImageDrawing(relationshipId, widthEmu, heightEmu))));
    }

    private static PartTypeInfo? ImagePartTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ImagePartType.Png,
        ".jpg" or ".jpeg" => ImagePartType.Jpeg,
        ".gif" => ImagePartType.Gif,
        ".bmp" => ImagePartType.Bmp,
        _ => null,
    };

    private static Drawing BuildImageDrawing(string relationshipId, long cx, long cy) => new(
        new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = "Picture" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Picture" },
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

    private static void AppendFootnotesPart(MainDocumentPart mainPart, FootnoteGroup group)
    {
        var footnotes = group.OfType<MdFootnote>().OrderBy(f => f.Order).ToList();
        if (footnotes.Count == 0)
            return;

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

        foreach (var footnote in footnotes)
        {
            var paragraphs = new List<OpenXmlElement>();
            var isFirstParagraph = true;

            foreach (var noteBlock in footnote.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
            {
                var paragraph = new Paragraph();
                if (isFirstParagraph)
                {
                    paragraph.Append(new Run(new RunProperties(new RunStyle { Val = "FootnoteText" }), new FootnoteReferenceMark()));
                    isFirstParagraph = false;
                }
                AppendInlines(paragraph, noteBlock.Inline!, mainPart, bold: false, italic: false);
                paragraphs.Add(paragraph);
            }

            if (paragraphs.Count == 0)
                paragraphs.Add(new Paragraph(new Run(new RunProperties(new RunStyle { Val = "FootnoteText" }), new FootnoteReferenceMark())));

            footnotesRoot.Append(new DocumentFormat.OpenXml.Wordprocessing.Footnote(paragraphs) { Id = footnote.Order });
        }

        footnotesPart.Footnotes = footnotesRoot;
    }

    private static void AppendInlines(
        Paragraph parent, ContainerInline? container, MainDocumentPart mainPart, bool bold, bool italic,
        bool strikethrough = false, bool underline = false, bool highlight = false, bool subscript = false, bool superscript = false)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    parent.Append(RunFor(literal.Content.ToString(), bold, italic, strikethrough, underline, highlight, subscript, superscript));
                    break;

                case CodeInline code:
                    parent.Append(RunFor(code.Content, bold, italic, strikethrough, underline, highlight, subscript, superscript));
                    break;

                // EmphasisExtras (~~strikethrough~~, ==highlight==, ~subscript~, ^superscript^,
                // ++inserted++) all parse as EmphasisInline too, distinguished only by
                // DelimiterChar — checking just DelimiterCount (as this used to) misreads
                // every one of them as bold/italic instead.
                case EmphasisInline emphasis:
                    AppendInlines(
                        parent, emphasis, mainPart,
                        bold: bold || (emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount is 2 or 3),
                        italic: italic || (emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount is 1 or 3),
                        strikethrough: strikethrough || emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2,
                        underline: underline || emphasis.DelimiterChar == '+',
                        highlight: highlight || emphasis.DelimiterChar == '=',
                        subscript: subscript || emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 1,
                        superscript: superscript || emphasis.DelimiterChar == '^' && emphasis.DelimiterCount == 1);
                    break;

                case LinkInline { IsImage: false } link:
                    var relationshipId = mainPart.AddHyperlinkRelationship(
                        new Uri(link.Url ?? string.Empty, UriKind.RelativeOrAbsolute), true).Id;
                    parent.Append(new Hyperlink(RunFor(PlainText(link), bold, italic, strikethrough, underline, highlight, subscript, superscript)) { Id = relationshipId });
                    break;

                case FootnoteLink { IsBackLink: false } footnoteLink:
                    parent.Append(new Run(
                        new RunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript }),
                        new FootnoteReference { Id = footnoteLink.Footnote.Order }));
                    break;

                case TaskList taskList:
                    parent.Append(RunFor(taskList.Checked ? "☑ " : "☐ ", bold, italic, strikethrough, underline, highlight, subscript, superscript));
                    break;

                case LineBreakInline:
                    parent.Append(new Run(new Break()));
                    break;

                case ContainerInline nested:
                    AppendInlines(parent, nested, mainPart, bold, italic, strikethrough, underline, highlight, subscript, superscript);
                    break;
            }
        }
    }

    private static Run RunFor(
        string text, bool bold, bool italic,
        bool strikethrough = false, bool underline = false, bool highlight = false, bool subscript = false, bool superscript = false)
    {
        var run = new Run();
        var properties = new RunProperties();
        if (bold) properties.Append(new Bold());
        if (italic) properties.Append(new Italic());
        if (strikethrough) properties.Append(new Strike());
        if (underline) properties.Append(new Underline { Val = UnderlineValues.Single });
        if (highlight) properties.Append(new Highlight { Val = HighlightColorValues.Yellow });
        if (subscript) properties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });
        if (superscript) properties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
        if (properties.HasChildren)
            run.Append(properties);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static string PlainText(ContainerInline? container)
    {
        if (container is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content);
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case ContainerInline nested:
                    sb.Append(PlainText(nested));
                    break;
            }
        }
        return sb.ToString();
    }
}

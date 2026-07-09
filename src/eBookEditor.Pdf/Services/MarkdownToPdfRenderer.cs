using eBookEditor.Markdown.Services;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Renders a chapter/page's Markdown body into a QuestPDF column — headings, paragraphs with
/// bold/italic/strikethrough/highlight/subscript/superscript/link runs, task lists,
/// definition lists, bullet/numbered lists (as prefixed paragraphs), tables, images
/// (resolved against <paramref name="sourceDir"/>), fenced/indented code blocks, custom
/// containers (":::" fenced divs — rendered by recursing into their content; the CSS class
/// they carry is EPUB-only since this renderer has no stylesheet to consult), and footnotes.
/// Markdig collects every footnote definition for a document into one FootnoteGroup block at
/// the end — since each chapter is parsed independently, that naturally becomes a "Notes"
/// section at the end of each chapter here, the same way the EPUB's HTML renderer places
/// them. True page-bottom footnotes (reflowing exactly onto whichever physical page
/// referenced them) would need a custom paginator; out of proportion for this app, so this
/// is an endnotes-per-chapter rendering instead — a common, acceptable print convention.
/// </summary>
internal class MarkdownToPdfRenderer
{
    public void RenderMarkdownBody(ColumnDescriptor column, string markdown, string? sourceDir, string? sectionName = null, string? headingFontFamily = null)
    {
        // Registering the section on whichever content item happened to render first used to
        // miss entirely for chapters starting with a list/table/code block/footnote group/
        // definition list (those branches below call column.Item() directly, bypassing the
        // per-block section hookup) and ALWAYS missed for a chapter with zero blocks (an
        // unwritten "New Chapter" stub, still empty) — in both cases the TOC's page-number
        // lookup for that chapter had nothing to resolve, rendering a literal "?", and the
        // running header's "current chapter" tracking got stuck on whatever chapter registered
        // last. A zero-height marker item, emitted unconditionally regardless of what (if
        // anything) the chapter actually contains, guarantees every spine item's section is
        // registered exactly once.
        if (sectionName is not null)
            column.Item().Height(0).Section(sectionName).CaptureContentPosition(sectionName);

        var document = Markdig.Markdown.Parse(markdown, MarkdownPipelineFactory.Create());
        foreach (var block in document)
            RenderBlock(column, block, sourceDir, headingFontFamily);
    }

    private static void RenderBlock(ColumnDescriptor column, Block block, string? sourceDir, string? headingFontFamily)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch { 1 => 20f, 2 => 16f, _ => 13f };
                column.Item().PaddingTop(heading.Level == 1 ? 0 : 10).PaddingBottom(6).Text(text =>
                    RenderInlines(text, heading.Inline, bold: true, italic: false, fontSize, headingFontFamily));
                break;

            case ParagraphBlock { Inline: not null } paragraph when TryGetSoleImage(paragraph.Inline, out var imageLink):
                RenderImage(column.Item(), imageLink!, sourceDir);
                break;

            case ParagraphBlock { Inline: not null } paragraph:
                column.Item().PaddingBottom(8).Text(text =>
                    RenderInlines(text, paragraph.Inline, bold: false, italic: false, fontSize: 11));
                break;

            case ListBlock list:
                var index = 1;
                foreach (var listItem in list.OfType<ListItemBlock>())
                {
                    var prefix = list.IsOrdered ? $"{index}. " : "• ";
                    foreach (var itemBlock in listItem.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
                    {
                        column.Item().PaddingLeft(16).PaddingBottom(4).Text(text =>
                        {
                            text.Span(prefix);
                            RenderInlines(text, itemBlock.Inline!, bold: false, italic: false, fontSize: 11);
                        });
                    }
                    index++;
                }
                break;

            case Table table:
                RenderTable(column, table);
                break;

            case CodeBlock codeBlock:
                RenderCodeBlock(column, codeBlock);
                break;

            case FootnoteGroup group:
                RenderFootnotes(column, group);
                break;

            case DefinitionList definitionList:
                RenderDefinitionList(column, definitionList);
                break;

            case CustomContainer container:
                foreach (var child in container)
                    RenderBlock(column, child, sourceDir, headingFontFamily);
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderBlock(column, child, sourceDir, headingFontFamily);
                break;
        }
    }

    private static void RenderDefinitionList(ColumnDescriptor column, DefinitionList definitionList)
    {
        foreach (var item in definitionList.OfType<DefinitionItem>())
        {
            foreach (var child in item)
            {
                if (child is DefinitionTerm term)
                {
                    column.Item().PaddingTop(6).Text(text =>
                        RenderInlines(text, term.Inline, bold: true, italic: false, fontSize: 11));
                }
                else if (child is ParagraphBlock { Inline: not null } definition)
                {
                    column.Item().PaddingLeft(16).PaddingBottom(2).Text(text =>
                        RenderInlines(text, definition.Inline, bold: false, italic: false, fontSize: 11));
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

    private static void RenderImage(IContainer container, LinkInline imageLink, string? sourceDir)
    {
        if (sourceDir is null || string.IsNullOrWhiteSpace(imageLink.Url))
            return;

        var absolutePath = Path.GetFullPath(Path.Combine(sourceDir, imageLink.Url));
        if (!File.Exists(absolutePath))
            return;

        container.PaddingBottom(8).AlignCenter().Element(image => image.MaxWidth(320).Image(absolutePath).FitWidth());
    }

    private static void RenderTable(ColumnDescriptor column, Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        if (rows.Count == 0)
            return;

        var columnCount = table.ColumnDefinitions.Count > 0
            ? table.ColumnDefinitions.Count
            : rows[0].Count();

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
                foreach (var cell in row.OfType<TableCell>())
                {
                    var cellContainer = questTable.Cell().Row(rowIndex).Column(colIndex)
                        .Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4)
                        .Background(row.IsHeader ? Colors.Grey.Lighten3 : Colors.White);

                    cellContainer.Text(text =>
                    {
                        foreach (var cellBlock in cell.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
                            RenderInlines(text, cellBlock.Inline!, bold: row.IsHeader, italic: false, fontSize: 10);
                    });

                    colIndex++;
                }
                rowIndex++;
            }
        });
    }

    private static void RenderCodeBlock(ColumnDescriptor column, CodeBlock codeBlock)
    {
        var lines = new List<string>();
        for (var i = 0; i < codeBlock.Lines.Count; i++)
            lines.Add(codeBlock.Lines.Lines[i].Slice.ToString());

        column.Item().PaddingVertical(6).Background(Colors.Grey.Lighten4).Padding(8)
            .Text(string.Join("\n", lines)).FontFamily("Courier New").FontSize(9.5f);
    }

    private static void RenderFootnotes(ColumnDescriptor column, FootnoteGroup group)
    {
        var footnotes = group.OfType<Footnote>().OrderBy(f => f.Order).ToList();
        if (footnotes.Count == 0)
            return;

        column.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        column.Item().PaddingTop(6).PaddingBottom(4).Text("Notes").Bold().FontSize(11);

        foreach (var footnote in footnotes)
        {
            column.Item().PaddingBottom(4).Row(row =>
            {
                row.ConstantItem(18).Text($"{footnote.Order}.").FontSize(9);
                row.RelativeItem().Text(text =>
                {
                    foreach (var noteBlock in footnote.OfType<ParagraphBlock>().Where(p => p.Inline is not null))
                        RenderInlines(text, noteBlock.Inline!, bold: false, italic: false, fontSize: 9);
                });
            });
        }
    }

    private static void RenderInlines(
        TextDescriptor text, ContainerInline? container, bool bold, bool italic, float fontSize, string? fontFamily = null,
        bool strikethrough = false, bool underline = false, bool highlight = false, bool subscript = false, bool superscript = false)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Style(text.Span(literal.Content.ToString()), bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    break;

                case CodeInline code:
                    Style(text.Span(code.Content), bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    break;

                // EmphasisExtras (~~strikethrough~~, ==highlight==, ~subscript~, ^superscript^,
                // ++inserted++) all parse as EmphasisInline too, distinguished only by
                // DelimiterChar — checking just DelimiterCount (as this used to) misreads
                // every one of them as bold/italic instead.
                case EmphasisInline emphasis:
                    RenderInlines(
                        text, emphasis,
                        bold: bold || (emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount is 2 or 3),
                        italic: italic || (emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount is 1 or 3),
                        fontSize, fontFamily,
                        strikethrough: strikethrough || emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2,
                        underline: underline || emphasis.DelimiterChar == '+',
                        highlight: highlight || emphasis.DelimiterChar == '=',
                        subscript: subscript || emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 1,
                        superscript: superscript || emphasis.DelimiterChar == '^' && emphasis.DelimiterCount == 1);
                    break;

                case LinkInline { IsImage: false } link:
                    // LinkInline.Url is empty for a link CommonMark resolved as a reference
                    // rather than an inline destination (e.g. Markdig's AutoIdentifier extension
                    // implicitly registers every heading as a reference target under its own
                    // text) — the real URL lives behind GetDynamicUrl for that case. Render as
                    // plain (non-hyperlinked) text if even that resolves to nothing, rather than
                    // point a link at an empty href.
                    var resolvedLinkUrl = link.GetDynamicUrl?.Invoke() ?? link.Url;
                    if (string.IsNullOrWhiteSpace(resolvedLinkUrl))
                        Style(text.Span(PlainText(link)), bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    else
                        Style(text.Hyperlink(PlainText(link), resolvedLinkUrl), bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    break;

                case FootnoteLink { IsBackLink: false } footnoteLink:
                    text.Span(footnoteLink.Footnote.Order.ToString()).FontSize(fontSize * 0.75f).Superscript();
                    break;

                case TaskList taskList:
                    Style(text.Span(taskList.Checked ? "☑ " : "☐ "), bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    break;

                case LineBreakInline:
                    text.EmptyLine();
                    break;

                case ContainerInline nested:
                    RenderInlines(text, nested, bold, italic, fontSize, fontFamily, strikethrough, underline, highlight, subscript, superscript);
                    break;
            }
        }
    }

    private static void Style(
        TextSpanDescriptor span, bool bold, bool italic, float fontSize, string? fontFamily,
        bool strikethrough, bool underline, bool highlight, bool subscript, bool superscript)
    {
        span.FontSize(fontSize);
        if (fontFamily is not null) span.FontFamily(fontFamily);
        if (bold) span.Bold();
        if (italic) span.Italic();
        if (strikethrough) span.Strikethrough();
        if (underline) span.Underline();
        if (highlight) span.BackgroundColor(Colors.Yellow.Lighten2);
        if (subscript) span.Subscript();
        if (superscript) span.Superscript();
    }

    private static string PlainText(ContainerInline? container)
    {
        if (container is null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
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

using eBookEditor.Markdown.Services;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Renders a chapter/page's Markdown body into a QuestPDF column — headings, paragraphs with
/// bold/italic/link runs, bullet/numbered lists (as prefixed paragraphs), tables, images
/// (resolved against <paramref name="sourceDir"/>), fenced/indented code blocks, and
/// footnotes. Markdig collects every footnote definition for a document into one
/// FootnoteGroup block at the end — since each chapter is parsed independently, that
/// naturally becomes a "Notes" section at the end of each chapter here, the same way the
/// EPUB's HTML renderer places them. True page-bottom footnotes (reflowing exactly onto
/// whichever physical page referenced them) would need a custom paginator; out of proportion
/// for this app, so this is an endnotes-per-chapter rendering instead — a common, acceptable
/// print convention.
/// </summary>
internal class MarkdownToPdfRenderer
{
    public void RenderMarkdownBody(ColumnDescriptor column, string markdown, string? sourceDir, string? sectionName = null, string? headingFontFamily = null)
    {
        var document = Markdig.Markdown.Parse(markdown, MarkdownPipelineFactory.Create());

        var isFirstBlock = true;
        foreach (var block in document)
        {
            RenderBlock(column, block, isFirstBlock ? sectionName : null, sourceDir, headingFontFamily);
            isFirstBlock = false;
        }
    }

    private static void RenderBlock(ColumnDescriptor column, Block block, string? sectionName, string? sourceDir, string? headingFontFamily)
    {
        IContainer Item()
        {
            var item = column.Item();
            // CaptureContentPosition (queried back via DynamicContext.GetContentCapturedPositions
            // in PdfPageHeader) lets the running header look up which physical page this chapter
            // opened on, to show "the current chapter" on right-hand pages.
            return sectionName is null ? item : item.Section(sectionName).CaptureContentPosition(sectionName);
        }

        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch { 1 => 20f, 2 => 16f, _ => 13f };
                Item().PaddingTop(heading.Level == 1 ? 0 : 10).PaddingBottom(6).Text(text =>
                    RenderInlines(text, heading.Inline, bold: true, italic: false, fontSize, headingFontFamily));
                break;

            case ParagraphBlock { Inline: not null } paragraph when TryGetSoleImage(paragraph.Inline, out var imageLink):
                RenderImage(Item(), imageLink!, sourceDir);
                break;

            case ParagraphBlock { Inline: not null } paragraph:
                Item().PaddingBottom(8).Text(text =>
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

            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderBlock(column, child, null, sourceDir, headingFontFamily);
                break;
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

    private static void RenderInlines(TextDescriptor text, ContainerInline? container, bool bold, bool italic, float fontSize, string? fontFamily = null)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Style(text.Span(literal.Content.ToString()), bold, italic, fontSize, fontFamily);
                    break;

                case CodeInline code:
                    Style(text.Span(code.Content), bold, italic, fontSize, fontFamily);
                    break;

                case EmphasisInline emphasis:
                    RenderInlines(
                        text, emphasis,
                        bold: bold || emphasis.DelimiterCount is 2 or 3,
                        italic: italic || emphasis.DelimiterCount is 1 or 3,
                        fontSize, fontFamily);
                    break;

                case LinkInline { IsImage: false } link:
                    Style(text.Hyperlink(PlainText(link), link.Url ?? string.Empty), bold, italic, fontSize, fontFamily);
                    break;

                case FootnoteLink { IsBackLink: false } footnoteLink:
                    text.Span(footnoteLink.Footnote.Order.ToString()).FontSize(fontSize * 0.75f).Superscript();
                    break;

                case LineBreakInline:
                    text.EmptyLine();
                    break;

                case ContainerInline nested:
                    RenderInlines(text, nested, bold, italic, fontSize, fontFamily);
                    break;
            }
        }
    }

    private static void Style(TextSpanDescriptor span, bool bold, bool italic, float fontSize, string? fontFamily = null)
    {
        span.FontSize(fontSize);
        if (fontFamily is not null) span.FontFamily(fontFamily);
        if (bold) span.Bold();
        if (italic) span.Italic();
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

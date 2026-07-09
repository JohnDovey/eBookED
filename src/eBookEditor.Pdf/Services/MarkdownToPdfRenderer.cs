using eBookEditor.Markdown.Services;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Renders a chapter/page's Markdown body into a QuestPDF column — headings, paragraphs with
/// bold/italic/link runs, and bullet/numbered lists (as prefixed paragraphs, same
/// simplification as MarkdownToDocxConverter). Tables, images inside body text, and code
/// blocks aren't rendered yet.
/// </summary>
internal class MarkdownToPdfRenderer
{
    public void RenderMarkdownBody(ColumnDescriptor column, string markdown, string? sectionName = null)
    {
        var document = Markdig.Markdown.Parse(markdown, MarkdownPipelineFactory.Create());

        var isFirstBlock = true;
        foreach (var block in document)
        {
            RenderBlock(column, block, isFirstBlock ? sectionName : null);
            isFirstBlock = false;
        }
    }

    private static void RenderBlock(ColumnDescriptor column, Block block, string? sectionName)
    {
        IContainer Item()
        {
            var item = column.Item();
            return sectionName is null ? item : item.Section(sectionName);
        }

        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch { 1 => 20f, 2 => 16f, _ => 13f };
                Item().PaddingTop(heading.Level == 1 ? 0 : 10).PaddingBottom(6).Text(text =>
                    RenderInlines(text, heading.Inline, bold: true, italic: false, fontSize));
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

            case QuoteBlock quote:
                foreach (var child in quote)
                    RenderBlock(column, child, null);
                break;
        }
    }

    private static void RenderInlines(TextDescriptor text, ContainerInline? container, bool bold, bool italic, float fontSize)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Style(text.Span(literal.Content.ToString()), bold, italic, fontSize);
                    break;

                case CodeInline code:
                    Style(text.Span(code.Content), bold, italic, fontSize);
                    break;

                case EmphasisInline emphasis:
                    RenderInlines(
                        text, emphasis,
                        bold: bold || emphasis.DelimiterCount is 2 or 3,
                        italic: italic || emphasis.DelimiterCount is 1 or 3,
                        fontSize);
                    break;

                case LinkInline { IsImage: false } link:
                    Style(text.Hyperlink(PlainText(link), link.Url ?? string.Empty), bold, italic, fontSize);
                    break;

                case LineBreakInline:
                    text.EmptyLine();
                    break;

                case ContainerInline nested:
                    RenderInlines(text, nested, bold, italic, fontSize);
                    break;
            }
        }
    }

    private static void Style(TextSpanDescriptor span, bool bold, bool italic, float fontSize)
    {
        span.FontSize(fontSize);
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

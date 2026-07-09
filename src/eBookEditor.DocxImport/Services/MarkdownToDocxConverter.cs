using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eBookEditor.Markdown.Services;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Converts a chapter's Markdown body into a .docx file — the reverse of DocxImportService.
/// Handles headings (mapped to Word's built-in Heading1-3 styles, so a round-tripped export
/// re-imports with the same chapter structure), paragraphs with bold/italic/link runs, and
/// bullet/numbered lists. List items are rendered as "•"/"1." prefixed paragraphs rather than
/// native Word list numbering, which needs a full NumberingDefinitionsPart — a reasonable
/// simplification since the visual result reads the same. Tables and images aren't
/// round-tripped yet.
/// </summary>
public class MarkdownToDocxConverter
{
    public void ConvertToFile(string markdown, string title, string outputPath)
    {
        var document = Markdig.Markdown.Parse(markdown, MarkdownPipelineFactory.Create());

        using var wordDocument = WordprocessingDocument.Create(outputPath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(HeadingParagraph(title, "Title"));

        foreach (var block in document)
            AppendBlock(body, block, mainPart);

        mainPart.Document.Save();
    }

    private static void AppendBlock(Body body, Block block, MainDocumentPart mainPart)
    {
        switch (block)
        {
            case HeadingBlock heading:
                body.Append(HeadingParagraph(PlainText(heading.Inline), $"Heading{Math.Clamp(heading.Level, 1, 3)}"));
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

            case QuoteBlock quote:
                foreach (var child in quote)
                    AppendBlock(body, child, mainPart);
                break;
        }
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

    private static void AppendInlines(Paragraph parent, ContainerInline? container, MainDocumentPart mainPart, bool bold, bool italic)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    parent.Append(RunFor(literal.Content.ToString(), bold, italic));
                    break;

                case CodeInline code:
                    parent.Append(RunFor(code.Content, bold, italic));
                    break;

                case EmphasisInline emphasis:
                    AppendInlines(
                        parent, emphasis, mainPart,
                        bold: bold || emphasis.DelimiterCount is 2 or 3,
                        italic: italic || emphasis.DelimiterCount is 1 or 3);
                    break;

                case LinkInline { IsImage: false } link:
                    var relationshipId = mainPart.AddHyperlinkRelationship(
                        new Uri(link.Url ?? string.Empty, UriKind.RelativeOrAbsolute), true).Id;
                    parent.Append(new Hyperlink(RunFor(PlainText(link), bold, italic)) { Id = relationshipId });
                    break;

                case LineBreakInline:
                    parent.Append(new Run(new Break()));
                    break;

                case ContainerInline nested:
                    AppendInlines(parent, nested, mainPart, bold, italic);
                    break;
            }
        }
    }

    private static Run RunFor(string text, bool bold, bool italic)
    {
        var run = new Run();
        if (bold || italic)
        {
            var properties = new RunProperties();
            if (bold) properties.Append(new Bold());
            if (italic) properties.Append(new Italic());
            run.Append(properties);
        }
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

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace eBookEditor.Tests.DocxImport;

internal static class DocxFixtureBuilder
{
    public static string BuildSimpleDocx(string path, byte[]? embeddedImageBytes = null)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(Heading("Chapter One", "Heading1"));
        body.Append(Paragraph(
            Run("Hello "),
            Run("bold", bold: true),
            Run(" and "),
            Run("italic", italic: true),
            Run(" text.")));

        body.Append(Heading("A Subsection", "Heading2"));
        body.Append(Paragraph(Run("Subsection content.")));
        body.Append(BulletItem("First bullet"));
        body.Append(BulletItem("Second bullet"));

        if (embeddedImageBytes is not null)
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
            imagePart.FeedData(new MemoryStream(embeddedImageBytes));
            var relId = mainPart.GetIdOfPart(imagePart);
            body.Append(new Paragraph(new Run(BuildImageDrawing(relId))));
        }

        body.Append(Heading("Chapter Two", "Heading1"));
        body.Append(Paragraph(Run("Second chapter content.")));

        body.Append(new Paragraph(new Run(new Text("Chapter 3: The Finale"))));
        body.Append(Paragraph(Run("Third chapter content.")));

        mainPart.Document.Save();
        return path;
    }

    public static string BuildDocxWithTable(string path)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(Heading("Chapter One", "Heading1"));
        body.Append(Paragraph(Run("Before the table.")));
        body.Append(BuildTable(
            ["Name", "Role"],
            [["Jane Doe", "Author"], ["Ed Itor", "Editor"]]));
        body.Append(Paragraph(Run("After the table.")));

        mainPart.Document.Save();
        return path;
    }

    public static string BuildDocxWithHyperlink(string path)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        var relationshipId = mainPart.AddHyperlinkRelationship(new Uri("https://example.com"), true).Id;

        body.Append(Heading("Chapter One", "Heading1"));
        body.Append(new Paragraph(
            new Run(new Text("Visit ") { Space = SpaceProcessingModeValues.Preserve }),
            new Hyperlink(new Run(new Text("our site"))) { Id = relationshipId },
            new Run(new Text(" for more.") { Space = SpaceProcessingModeValues.Preserve })));

        mainPart.Document.Save();
        return path;
    }

    private static Table BuildTable(string[] header, string[][] rows)
    {
        var table = new Table();
        table.Append(BuildTableRow(header));
        foreach (var row in rows)
            table.Append(BuildTableRow(row));
        return table;
    }

    private static TableRow BuildTableRow(string[] cellTexts)
    {
        var row = new TableRow();
        foreach (var text in cellTexts)
            row.Append(new TableCell(new Paragraph(new Run(new Text(text)))));
        return row;
    }

    /// <summary>A hand-typed "Table of Contents" section — a heading followed by plain
    /// paragraphs that each read exactly like a chapter title, the way an author might
    /// manually copy their chapter list into the front of the manuscript.</summary>
    public static string BuildDocxWithHandTypedToc(string path)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(Heading("Table of Contents", "Heading1"));
        body.Append(Paragraph(Run("Chapter 1: Getting Ready")));
        body.Append(Paragraph(Run("Chapter 2: Starting Out")));
        body.Append(Paragraph(Run("Chapter 3: The Finale")));

        body.Append(Heading("Chapter 1: Getting Ready", "Heading1"));
        body.Append(Paragraph(Run("Real content for chapter one.")));
        body.Append(Heading("Chapter 2: Starting Out", "Heading1"));
        body.Append(Paragraph(Run("Real content for chapter two.")));

        mainPart.Document.Save();
        return path;
    }

    /// <summary>Word's own Insert &gt; Table of Contents field: each entry is a paragraph
    /// styled "TOC1" (page-number leader collapsed into plain text here, since only the style
    /// matters for the filtering this fixture exercises).</summary>
    public static string BuildDocxWithFieldGeneratedToc(string path)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(Heading("Contents", "Heading1"));
        body.Append(Heading("Chapter 1: Getting Ready\t1", "TOC1"));
        body.Append(Heading("Chapter 2: Starting Out\t5", "TOC1"));

        body.Append(Heading("Chapter 1: Getting Ready", "Heading1"));
        body.Append(Paragraph(Run("Real content for chapter one.")));
        body.Append(Heading("Chapter 2: Starting Out", "Heading1"));
        body.Append(Paragraph(Run("Real content for chapter two.")));

        mainPart.Document.Save();
        return path;
    }

    public static string BuildDocxWithPreamble(string path)
    {
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();
        mainPart.Document.Append(body);

        body.Append(Paragraph(Run("Some preamble text before any chapter heading.")));
        body.Append(Heading("Chapter One", "Heading1"));
        body.Append(Paragraph(Run("Chapter one content.")));

        mainPart.Document.Save();
        return path;
    }

    private static Paragraph Heading(string text, string styleId) => new(
        new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
        new Run(new Text(text)));

    private static Paragraph Paragraph(params Run[] runs) => new(runs.Cast<DocumentFormat.OpenXml.OpenXmlElement>());

    private static Paragraph BulletItem(string text) => new(
        new ParagraphProperties(new NumberingProperties(
            new NumberingLevelReference { Val = 0 },
            new NumberingId { Val = 1 })),
        new Run(new Text(text)));

    private static Run Run(string text, bool bold = false, bool italic = false)
    {
        var run = new Run();
        if (bold || italic)
        {
            var runProperties = new RunProperties();
            if (bold) runProperties.Append(new Bold());
            if (italic) runProperties.Append(new Italic());
            run.Append(runProperties);
        }
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static Drawing BuildImageDrawing(string relId) => new(
        new DW.Inline(
            new DW.Extent { Cx = 990000L, Cy = 792000L },
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
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = 990000L, Cy = 792000L }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })
                    )
                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
        )
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        });
}

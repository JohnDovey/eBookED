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

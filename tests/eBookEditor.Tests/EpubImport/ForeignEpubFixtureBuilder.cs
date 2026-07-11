using System.IO.Compression;
using System.Text;

namespace eBookEditor.Tests.EpubImport;

/// <summary>Hand-builds a minimal but structurally real EPUB3 file with its own stylesheet,
/// a plain cross-chapter link, a cross-chapter link into a specific heading (chapter1's
/// "#section-a" link into chapter2's own "id=section-a" heading), and a plain same-page
/// footnote — deliberately NOT using this app's own EpubBuilder, so tests exercise the
/// importer against a genuinely foreign producer's layout (a nested OEBPS/text/ directory, its
/// own CSS, its own footnote markup) rather than round-tripping this app's own conventions.</summary>
internal static class ForeignEpubFixtureBuilder
{
    public static string Build(string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        using var stream = new FileStream(path, FileMode.CreateNew);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteEntry(archive, "mimetype", "application/epub+zip");
        WriteEntry(archive, "META-INF/container.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);

        WriteEntry(archive, "OEBPS/styles/style.css", """
            body { font-family: "Georgia Foreign", serif; }
            h1 { font-family: "Georgia Foreign", serif; font-weight: bold; }
            """);

        WriteEntry(archive, "OEBPS/text/chapter1.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>Chapter One</title><link rel="stylesheet" type="text/css" href="../styles/style.css"/></head>
            <body>
            <h1>Chapter One</h1>
            <p>This is the first chapter. See <a href="chapter2.xhtml">the next chapter</a> for more.</p>
            <p>Jump straight to <a href="chapter2.xhtml#section-a">Section A</a> in the next chapter.</p>
            <p>Here is a note.<sup><a href="#note1">1</a></sup></p>
            <p id="note1">This is the footnote text.</p>
            </body>
            </html>
            """);

        WriteEntry(archive, "OEBPS/text/chapter2.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>Chapter Two</title><link rel="stylesheet" type="text/css" href="../styles/style.css"/></head>
            <body>
            <h1>Chapter Two</h1>
            <p>This is the second chapter.</p>
            <h2 id="section-a">Section A</h2>
            <p>Section A's own content.</p>
            </body>
            </html>
            """);

        WriteEntry(archive, "OEBPS/nav.xhtml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
            <head><title>Table of Contents</title></head>
            <body>
            <nav epub:type="toc" id="toc">
              <ol>
                <li><a href="text/chapter1.xhtml">Chapter One</a></li>
                <li><a href="text/chapter2.xhtml">Chapter Two</a></li>
              </ol>
            </nav>
            <nav epub:type="landmarks" hidden="">
              <ol>
                <li><a epub:type="bodymatter" href="text/chapter1.xhtml">Start of Content</a></li>
              </ol>
            </nav>
            </body>
            </html>
            """);

        WriteEntry(archive, "OEBPS/content.opf", """
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="pub-id">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:identifier id="pub-id">urn:uuid:12345678-1234-1234-1234-123456789012</dc:identifier>
                <dc:title>Foreign Test Book</dc:title>
                <dc:language>en</dc:language>
                <dc:creator id="creator1">Jane Foreign</dc:creator>
              </metadata>
              <manifest>
                <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                <item id="chapter1" href="text/chapter1.xhtml" media-type="application/xhtml+xml"/>
                <item id="chapter2" href="text/chapter2.xhtml" media-type="application/xhtml+xml"/>
                <item id="style" href="styles/style.css" media-type="text/css"/>
              </manifest>
              <spine>
                <itemref idref="chapter1"/>
                <itemref idref="chapter2"/>
              </spine>
            </package>
            """);

        return path;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }
}

using System.Xml.Linq;

namespace eBookEditor.Epub.Services;

internal static class EpubContainerWriter
{
    public static string Build()
    {
        XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "container",
                new XAttribute("version", "1.0"),
                new XElement(ns + "rootfiles",
                    new XElement(ns + "rootfile",
                        new XAttribute("full-path", "OEBPS/package.opf"),
                        new XAttribute("media-type", "application/oebps-package+xml")))));

        return XmlOutput.ToXmlString(doc);
    }
}

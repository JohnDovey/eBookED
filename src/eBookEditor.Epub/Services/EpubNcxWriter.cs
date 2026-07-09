using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.Epub.Models;

namespace eBookEditor.Epub.Services;

internal static class EpubNcxWriter
{
    private static readonly XNamespace Ncx = "http://www.daisy.org/z3986/2005/ncx/";

    public static string Build(string bookTitle, string uniqueIdentifier, IReadOnlyList<EpubContentDoc> contentDocs)
    {
        var navPoints = contentDocs.Select((doc, index) =>
        {
            var label = doc.SpineItem is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {doc.SpineItem.ResolvedNumber}: {doc.Title}"
                : doc.Title;

            return new XElement(Ncx + "navPoint",
                new XAttribute("id", $"navpoint-{index + 1}"),
                new XAttribute("playOrder", index + 1),
                new XElement(Ncx + "navLabel", new XElement(Ncx + "text", label)),
                new XElement(Ncx + "content", new XAttribute("src", doc.FileName)));
        });

        var doc2 = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ncx + "ncx",
                new XAttribute("version", "2005-1"),
                new XElement(Ncx + "head",
                    new XElement(Ncx + "meta", new XAttribute("name", "dtb:uid"), new XAttribute("content", uniqueIdentifier)),
                    new XElement(Ncx + "meta", new XAttribute("name", "dtb:depth"), new XAttribute("content", "1")),
                    new XElement(Ncx + "meta", new XAttribute("name", "dtb:totalPageCount"), new XAttribute("content", "0")),
                    new XElement(Ncx + "meta", new XAttribute("name", "dtb:maxPageNumber"), new XAttribute("content", "0"))),
                new XElement(Ncx + "docTitle", new XElement(Ncx + "text", bookTitle)),
                new XElement(Ncx + "navMap", navPoints)));

        return XmlOutput.ToXmlString(doc2);
    }
}

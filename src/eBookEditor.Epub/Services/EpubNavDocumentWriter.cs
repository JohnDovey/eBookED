using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.Epub.Models;

namespace eBookEditor.Epub.Services;

internal static class EpubNavDocumentWriter
{
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace Epub = "http://www.idpf.org/2007/ops";

    public static string Build(IReadOnlyList<EpubContentDoc> contentDocs)
    {
        var listItems = contentDocs.Select(doc =>
        {
            var label = doc.SpineItem is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {doc.SpineItem.ResolvedNumber}: {doc.Title}"
                : doc.Title;

            return new XElement(Xhtml + "li",
                new XElement(Xhtml + "a", new XAttribute("href", doc.FileName), label));
        });

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("html", null, null, null),
            new XElement(Xhtml + "html",
                new XAttribute(XNamespace.Xmlns + "epub", Epub.NamespaceName),
                new XElement(Xhtml + "head",
                    new XElement(Xhtml + "title", "Table of Contents"),
                    new XElement(Xhtml + "meta", new XAttribute("charset", "utf-8"))),
                new XElement(Xhtml + "body",
                    new XElement(Xhtml + "nav",
                        new XAttribute(Epub + "type", "toc"),
                        new XAttribute("id", "toc"),
                        new XElement(Xhtml + "h1", "Table of Contents"),
                        new XElement(Xhtml + "ol", listItems)))));

        return XmlOutput.ToXmlString(doc);
    }
}

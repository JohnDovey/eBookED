using System.Xml.Linq;

namespace eBookEditor.Epub.Services;

internal static class EpubXhtmlContentWriter
{
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace Epub = "http://www.idpf.org/2007/ops";

    public static string Wrap(string title, string epubType, string bodyHtml)
    {
        var safeHtml = HtmlXmlSafety.MakeXmlSafe(bodyHtml);
        var bodyFragment = XElement.Parse($"<div xmlns=\"{Xhtml}\">{safeHtml}</div>", LoadOptions.PreserveWhitespace);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("html", null, null, null),
            new XElement(Xhtml + "html",
                new XAttribute(XNamespace.Xmlns + "epub", Epub.NamespaceName),
                new XElement(Xhtml + "head",
                    new XElement(Xhtml + "title", title),
                    new XElement(Xhtml + "meta", new XAttribute("charset", "utf-8")),
                    new XElement(Xhtml + "link",
                        new XAttribute("rel", "stylesheet"),
                        new XAttribute("type", "text/css"),
                        new XAttribute("href", "styles.css"))),
                new XElement(Xhtml + "body",
                    new XAttribute(Epub + "type", epubType),
                    bodyFragment)));

        return XmlOutput.ToXmlString(doc);
    }
}

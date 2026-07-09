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
                        new XElement(Xhtml + "ol", listItems)),
                    BuildLandmarksNav(contentDocs))));

        return XmlOutput.ToXmlString(doc);
    }

    /// <summary>
    /// A "nav epub:type=landmarks" pointing at the title page, the table of contents, and —
    /// critically for Kindle/KDP — the first real chapter tagged "bodymatter", which reading
    /// systems use to decide where a book should actually open (skipping front matter). This
    /// is the modern EPUB3-native mechanism Vellum and Amazon's own guidance both use, in
    /// place of the older/deprecated OPF &lt;guide&gt; element.
    /// </summary>
    private static XElement? BuildLandmarksNav(IReadOnlyList<EpubContentDoc> contentDocs)
    {
        var entries = new List<XElement>();

        var titlePage = contentDocs.FirstOrDefault(d => d.EpubType == "titlepage");
        if (titlePage is not null)
            entries.Add(LandmarkItem(titlePage.FileName, "titlepage", "Title Page"));

        var tocPage = contentDocs.FirstOrDefault(d => d.EpubType == "toc");
        if (tocPage is not null)
            entries.Add(LandmarkItem(tocPage.FileName, "toc", "Table of Contents"));

        var firstChapter = contentDocs.FirstOrDefault(d => d.EpubType == "chapter");
        if (firstChapter is not null)
            entries.Add(LandmarkItem(firstChapter.FileName, "bodymatter", "Start of Content"));

        if (entries.Count == 0)
            return null;

        return new XElement(Xhtml + "nav",
            new XAttribute(Epub + "type", "landmarks"),
            new XAttribute("id", "landmarks"),
            new XAttribute("hidden", "hidden"),
            new XElement(Xhtml + "ol", entries));
    }

    private static XElement LandmarkItem(string href, string epubType, string label) =>
        new(Xhtml + "li",
            new XElement(Xhtml + "a", new XAttribute("href", href), new XAttribute(Epub + "type", epubType), label));
}

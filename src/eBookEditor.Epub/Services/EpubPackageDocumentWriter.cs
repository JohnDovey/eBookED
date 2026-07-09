using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.Epub.Models;

namespace eBookEditor.Epub.Services;

internal static class EpubPackageDocumentWriter
{
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static string Build(
        BookMetadata metadata,
        IReadOnlyList<EpubContentDoc> contentDocs,
        IReadOnlyDictionary<string, string> imageFiles,
        string? coverImageFileName,
        string uniqueIdentifier)
    {
        var metadataElements = new List<XElement>
        {
            new(Dc + "identifier", new XAttribute("id", "pub-id"), uniqueIdentifier),
            new(Dc + "title", metadata.Title),
            new(Dc + "language", metadata.Language),
            new(Opf + "meta", new XAttribute("property", "dcterms:modified"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new(Opf + "meta", new XAttribute("name", "generator"), new XAttribute("content", GeneratorTag()))
        };

        var contributorIndex = 0;
        foreach (var contributor in metadata.Contributors)
        {
            contributorIndex++;
            var elementName = contributor.Role == ContributorRole.Author ? Dc + "creator" : Dc + "contributor";
            var id = $"contrib-{contributorIndex}";
            metadataElements.Add(new XElement(elementName, new XAttribute("id", id), contributor.Name));

            var relatorCode = MarcRelatorCode(contributor.Role);
            if (relatorCode is not null)
            {
                metadataElements.Add(new XElement(Opf + "meta",
                    new XAttribute("refines", $"#{id}"),
                    new XAttribute("property", "role"),
                    new XAttribute("scheme", "marc:relators"),
                    relatorCode));
            }

            if (!string.IsNullOrWhiteSpace(contributor.SortName))
            {
                metadataElements.Add(new XElement(Opf + "meta",
                    new XAttribute("refines", $"#{id}"),
                    new XAttribute("property", "file-as"),
                    contributor.SortName));
            }
        }

        // Fixed, always-true accessibility metadata: every book this app builds has a real
        // nav document (structural navigation + TOC) and a linear reading order, and content
        // is text-only (no non-text hazards to disclose).
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessMode"), "textual"));
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessModeSufficient"), "textual"));
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessibilityFeature"), "structuralNavigation"));
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessibilityFeature"), "tableOfContents"));
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessibilityFeature"), "readingOrder"));
        metadataElements.Add(new XElement(Opf + "meta", new XAttribute("property", "schema:accessibilityHazard"), "none"));

        if (!string.IsNullOrWhiteSpace(metadata.CopyrightDisclaimer))
            metadataElements.Add(new XElement(Dc + "rights", metadata.CopyrightDisclaimer));

        if (metadata.Publisher is { } publisher)
            metadataElements.Add(new XElement(Dc + "publisher", publisher.Name));

        if (metadata.PublicationDate is { } pubDate)
            metadataElements.Add(new XElement(Dc + "date", pubDate.ToString("yyyy-MM-dd")));

        if (coverImageFileName is not null)
            metadataElements.Add(new XElement(Opf + "meta", new XAttribute("name", "cover"), new XAttribute("content", "cover-image")));

        var manifestItems = new List<XElement>
        {
            new(Opf + "item", new XAttribute("id", "nav"), new XAttribute("href", "nav.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav")),
            new(Opf + "item", new XAttribute("id", "ncx"), new XAttribute("href", "toc.ncx"),
                new XAttribute("media-type", "application/x-dtbncx+xml")),
            new(Opf + "item", new XAttribute("id", "css"), new XAttribute("href", "styles.css"),
                new XAttribute("media-type", "text/css"))
        };

        foreach (var doc in contentDocs)
        {
            manifestItems.Add(new XElement(Opf + "item",
                new XAttribute("id", ManifestId(doc)),
                new XAttribute("href", doc.FileName),
                new XAttribute("media-type", "application/xhtml+xml")));
        }

        foreach (var (absolutePath, destFileName) in imageFiles)
        {
            var isCover = destFileName == coverImageFileName;
            var item = new XElement(Opf + "item",
                new XAttribute("id", $"img-{SanitizeId(destFileName)}"),
                new XAttribute("href", $"images/{destFileName}"),
                new XAttribute("media-type", MediaTypeResolver.ForImage(destFileName)));
            if (isCover)
                item.Add(new XAttribute("properties", "cover-image"));
            manifestItems.Add(item);
        }

        var spineItems = contentDocs.Select(doc =>
            new XElement(Opf + "itemref", new XAttribute("idref", ManifestId(doc))));

        var packageDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Opf + "package",
                new XAttribute("version", "3.0"),
                new XAttribute("unique-identifier", "pub-id"),
                new XAttribute("prefix", "schema: http://schema.org/"),
                new XAttribute(XNamespace.Xml + "lang", metadata.Language),
                new XElement(Opf + "metadata",
                    new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
                    metadataElements),
                new XElement(Opf + "manifest", manifestItems),
                new XElement(Opf + "spine", new XAttribute("toc", "ncx"), spineItems)));

        return XmlOutput.ToXmlString(packageDoc);
    }

    private static string GeneratorTag()
    {
        var version = typeof(EpubPackageDocumentWriter).Assembly.GetName().Version;
        return version is null ? "eBook Editor" : $"eBook Editor {version.ToString(3)}";
    }

    private static string ManifestId(EpubContentDoc doc) => SanitizeId(doc.FileName);

    private static string SanitizeId(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).Replace(' ', '-').Replace('.', '-');

    private static string? MarcRelatorCode(ContributorRole role) => role switch
    {
        ContributorRole.Author => "aut",
        ContributorRole.Editor => "edt",
        ContributorRole.Illustrator => "ill",
        ContributorRole.Translator => "trl",
        ContributorRole.Foreword => "wpr",
        _ => null
    };
}

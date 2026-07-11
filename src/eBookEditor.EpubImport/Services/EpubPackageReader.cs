using System.IO.Compression;
using System.Xml.Linq;
using eBookEditor.Core.Models;
using eBookEditor.EpubImport.Models;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Reads an arbitrary EPUB's container/package (OPF) documents into an EpubPackage. Locates
/// the OPF via META-INF/container.xml rather than assuming a fixed path like "OEBPS/
/// package.opf" (this app's own export convention, not a spec requirement) — a real-world
/// EPUB from another tool may put it anywhere.
/// </summary>
public static class EpubPackageReader
{
    private static readonly XNamespace Container = "urn:oasis:names:tc:opendocument:xmlns:container";
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static EpubPackage Read(ZipArchive archive)
    {
        var opfPath = ReadOpfPath(archive);
        var opfEntry = archive.GetEntry(opfPath)
            ?? throw new InvalidDataException($"EPUB package document not found at '{opfPath}'.");

        using var opfStream = opfEntry.Open();
        var opfDoc = XDocument.Load(opfStream);
        var opfDirectory = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? "";

        var packageEl = opfDoc.Root
            ?? throw new InvalidDataException("EPUB package document has no root element.");
        var metadataEl = packageEl.Element(Opf + "metadata")
            ?? throw new InvalidDataException("EPUB package document has no <metadata>.");
        var manifestEl = packageEl.Element(Opf + "manifest")
            ?? throw new InvalidDataException("EPUB package document has no <manifest>.");
        var spineEl = packageEl.Element(Opf + "spine")
            ?? throw new InvalidDataException("EPUB package document has no <spine>.");

        var metadata = ReadMetadata(metadataEl);
        var legacyCoverManifestId = metadataEl.Elements(Opf + "meta")
            .FirstOrDefault(m => (string?)m.Attribute("name") == "cover")
            ?.Attribute("content")?.Value;

        var manifestById = manifestEl.Elements(Opf + "item")
            .Select(item =>
            {
                var id = item.Attribute("id")?.Value ?? "";
                var properties = item.Attribute("properties")?.Value;
                var isCover = (properties?.Split(' ').Contains("cover-image") ?? false) || id == legacyCoverManifestId;
                return new EpubManifestItem(id, item.Attribute("href")?.Value ?? "", item.Attribute("media-type")?.Value ?? "", properties, isCover);
            })
            .Where(item => item.Id.Length > 0)
            .ToDictionary(item => item.Id);

        var spineItemIds = spineEl.Elements(Opf + "itemref")
            .Select(itemref => itemref.Attribute("idref")?.Value)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToList();

        return new EpubPackage(metadata, manifestById, spineItemIds, opfDirectory);
    }

    private static string ReadOpfPath(ZipArchive archive)
    {
        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidDataException("Not a valid EPUB file: missing META-INF/container.xml.");

        using var containerStream = containerEntry.Open();
        var containerDoc = XDocument.Load(containerStream);
        var rootFile = containerDoc.Descendants(Container + "rootfile").FirstOrDefault()
            ?? throw new InvalidDataException("META-INF/container.xml has no <rootfile>.");

        return rootFile.Attribute("full-path")?.Value
            ?? throw new InvalidDataException("<rootfile> has no full-path attribute.");
    }

    private static BookMetadata ReadMetadata(XElement metadataEl)
    {
        var title = metadataEl.Element(Dc + "title")?.Value?.Trim() ?? "";
        var language = metadataEl.Element(Dc + "language")?.Value?.Trim();
        var publisher = metadataEl.Element(Dc + "publisher")?.Value?.Trim();
        var rights = metadataEl.Element(Dc + "rights")?.Value?.Trim();
        var description = metadataEl.Element(Dc + "description")?.Value?.Trim();
        var contributors = ReadContributors(metadataEl);
        var (isbn10, isbn13) = ReadIsbn(metadataEl);

        return new BookMetadata
        {
            Title = title,
            Language = string.IsNullOrWhiteSpace(language) ? "en" : language,
            Contributors = contributors,
            Publisher = string.IsNullOrWhiteSpace(publisher) ? null : new PublisherInfo(publisher),
            CopyrightDisclaimer = string.IsNullOrWhiteSpace(rights) ? BookMetadata.DefaultDisclaimerText : rights,
            Blurb = string.IsNullOrWhiteSpace(description) ? null : description,
            Isbn10 = isbn10,
            Isbn13 = isbn13,
            CopyrightHolder = contributors.FirstOrDefault(c => c.Role == ContributorRole.Author)?.Name ?? ""
        };
    }

    private static List<Contributor> ReadContributors(XElement metadataEl)
    {
        var entries = metadataEl.Elements(Dc + "creator").Select(el => (Element: el, DefaultRole: ContributorRole.Author))
            .Concat(metadataEl.Elements(Dc + "contributor").Select(el => (Element: el, DefaultRole: ContributorRole.Other)));

        var contributors = new List<Contributor>();
        foreach (var (element, defaultRole) in entries)
        {
            var id = element.Attribute("id")?.Value;
            var role = defaultRole;
            if (id is not null)
            {
                var roleMeta = metadataEl.Elements(Opf + "meta")
                    .FirstOrDefault(m => (string?)m.Attribute("refines") == $"#{id}" && (string?)m.Attribute("property") == "role");
                if (roleMeta is not null)
                    role = RoleFromMarcCode(roleMeta.Value.Trim());
            }

            var (firstName, lastName) = SplitName(element.Value.Trim());
            if (firstName.Length > 0 || lastName.Length > 0)
                contributors.Add(new Contributor(firstName, lastName, role));
        }

        return contributors;
    }

    private static ContributorRole RoleFromMarcCode(string code) => code switch
    {
        "aut" => ContributorRole.Author,
        "edt" => ContributorRole.Editor,
        "ill" => ContributorRole.Illustrator,
        "trl" => ContributorRole.Translator,
        "wpr" => ContributorRole.Foreword,
        _ => ContributorRole.Other
    };

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var lastSpace = fullName.LastIndexOf(' ');
        return lastSpace < 0 ? (fullName, "") : (fullName[..lastSpace], fullName[(lastSpace + 1)..]);
    }

    private static (string? Isbn10, string? Isbn13) ReadIsbn(XElement metadataEl)
    {
        foreach (var identifier in metadataEl.Elements(Dc + "identifier"))
        {
            var value = identifier.Value.Trim();
            if (!value.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase))
                continue;

            var isbn = value["urn:isbn:".Length..].Trim();
            return isbn.Length == 10 ? (isbn, null) : (null, isbn);
        }

        return (null, null);
    }
}

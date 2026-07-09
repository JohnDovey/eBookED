using System.IO.Compression;
using System.Xml;

namespace eBookEditor.Epub.Services;

public record EpubValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// Structural sanity check, not a substitute for a full EPUB 3.3 conformance validator
/// (e.g. epubcheck) — confirms the mechanical requirements that are easy to get wrong by
/// hand (mimetype first and stored, well-formed XML documents) rather than deep semantic
/// conformance.
/// </summary>
public static class EpubValidationHelper
{
    public static EpubValidationResult Validate(string epubPath)
    {
        var errors = new List<string>();

        using var archive = ZipFile.OpenRead(epubPath);
        var entries = archive.Entries.ToList();

        if (entries.Count == 0 || entries[0].FullName != "mimetype")
            errors.Add("First entry in the archive must be 'mimetype'.");
        else
        {
            var mimetypeEntry = entries[0];
            if (mimetypeEntry.CompressedLength != mimetypeEntry.Length)
                errors.Add("'mimetype' entry must be stored (uncompressed).");

            using var reader = new StreamReader(mimetypeEntry.Open());
            var content = reader.ReadToEnd();
            if (content != "application/epub+zip")
                errors.Add($"'mimetype' entry must contain exactly 'application/epub+zip', found '{content}'.");
        }

        if (archive.GetEntry("META-INF/container.xml") is null)
            errors.Add("Missing META-INF/container.xml.");

        if (archive.GetEntry("OEBPS/package.opf") is null)
            errors.Add("Missing OEBPS/package.opf.");

        if (archive.GetEntry("OEBPS/nav.xhtml") is null)
            errors.Add("Missing OEBPS/nav.xhtml.");

        foreach (var entry in entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                                                   e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                                                   e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase) ||
                                                   e.FullName.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var entryStream = entry.Open();
                using var xmlReader = XmlReader.Create(entryStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                while (xmlReader.Read())
                {
                }
            }
            catch (XmlException ex)
            {
                errors.Add($"'{entry.FullName}' is not well-formed XML: {ex.Message}");
            }
        }

        return new EpubValidationResult(errors.Count == 0, errors);
    }
}

using eBookEditor.Core.Models;

namespace eBookEditor.EpubImport.Models;

public record EpubManifestItem(string Id, string Href, string MediaType, string? Properties, bool IsCoverImage);

/// <summary>The parsed shape of an EPUB's package document (OPF) — metadata, every manifest
/// item keyed by its manifest id, the spine's reading order (a list of manifest ids), and the
/// OPF's own directory (every manifest href is relative to this, not a hardcoded "OEBPS/"
/// prefix — that's this app's own export convention, not a spec requirement).</summary>
public record EpubPackage(
    BookMetadata Metadata,
    IReadOnlyDictionary<string, EpubManifestItem> ManifestById,
    IReadOnlyList<string> SpineItemIds,
    string OpfDirectory);

/// <summary>Per-chapter titles (keyed by manifest href, fragment stripped) and landmark types
/// (also keyed by href) read from nav.xhtml (EPUB3) or toc.ncx (EPUB2) — landmarks only cover
/// a handful of structural waypoints (title page, TOC, first real chapter), not every item.</summary>
public record EpubNavigationInfo(
    IReadOnlyDictionary<string, string> TitlesByHref,
    IReadOnlyDictionary<string, string> LandmarkTypesByHref);

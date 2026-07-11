using System.Xml.Linq;
using eBookEditor.EpubImport.Models;

namespace eBookEditor.EpubImport.Services;

/// <summary>
/// Reads per-chapter titles and structural landmarks from an EPUB3 nav document (nav.xhtml —
/// the counterpart to this app's own EpubNavDocumentWriter) or an EPUB2 NCX document
/// (toc.ncx — EpubNcxWriter's counterpart), whichever the source EPUB provides.
/// </summary>
public static class EpubNavigationReader
{
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace Epub = "http://www.idpf.org/2007/ops";
    private static readonly XNamespace Ncx = "http://www.daisy.org/z3986/2005/ncx/";

    public static EpubNavigationInfo ReadNav(XDocument navDoc)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var landmarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var navs = navDoc.Descendants(Xhtml + "nav").ToList();

        var tocNav = navs.FirstOrDefault(n => (string?)n.Attribute(Epub + "type") == "toc") ?? navs.FirstOrDefault();
        if (tocNav is not null)
        {
            foreach (var a in tocNav.Descendants(Xhtml + "a"))
            {
                var href = a.Attribute("href")?.Value;
                if (string.IsNullOrWhiteSpace(href))
                    continue;
                titles[StripFragment(href)] = a.Value.Trim();
            }
        }

        var landmarksNav = navs.FirstOrDefault(n => (string?)n.Attribute(Epub + "type") == "landmarks");
        if (landmarksNav is not null)
        {
            foreach (var a in landmarksNav.Descendants(Xhtml + "a"))
            {
                var href = a.Attribute("href")?.Value;
                var type = (string?)a.Attribute(Epub + "type");
                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(type))
                    continue;
                landmarks[StripFragment(href)] = type;
            }
        }

        return new EpubNavigationInfo(titles, landmarks);
    }

    public static EpubNavigationInfo ReadNcx(XDocument ncxDoc)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var navMap = ncxDoc.Descendants(Ncx + "navMap").FirstOrDefault();
        if (navMap is null)
            return new EpubNavigationInfo(titles, new Dictionary<string, string>());

        foreach (var navPoint in navMap.Descendants(Ncx + "navPoint"))
        {
            var label = navPoint.Element(Ncx + "navLabel")?.Element(Ncx + "text")?.Value?.Trim();
            var src = navPoint.Element(Ncx + "content")?.Attribute("src")?.Value;
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(src))
                continue;

            titles[StripFragment(src)] = label;
        }

        return new EpubNavigationInfo(titles, new Dictionary<string, string>());
    }

    private static string StripFragment(string href) => href.Split('#')[0];
}

using System.Net;
using System.Text;
using eBookEditor.Core.Models;

namespace eBookEditor.Html.Services;

/// <summary>
/// Generates the HTML body fragments for the app's auto-generated front/back matter pages
/// (title, imprint/copyright, table of contents, about-the-author). Every value interpolated
/// from BookMetadata is HTML-encoded — unlike the old Markdown generator, raw "&amp;", "&lt;",
/// "&gt;", or quote characters in a title/author name would otherwise corrupt the markup itself,
/// not just render literally.
/// </summary>
public class PageGeneratorService
{
    public string GenerateTitlePage(BookMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<h1 class=\"centered-block\">{Encode(metadata.Title)}</h1>");

        if (!string.IsNullOrWhiteSpace(metadata.Subtitle))
            sb.AppendLine($"<h2 class=\"centered-block\">{Encode(metadata.Subtitle)}</h2>");

        var authorNames = metadata.Authors.Select(a => a.Name).ToList();
        if (authorNames.Count > 0)
            sb.AppendLine($"<p class=\"centered-block\"><em>by {Encode(string.Join(", ", authorNames))}</em></p>");

        var editorNames = metadata.Editors.Select(e => e.Name).ToList();
        var illustratorNames = metadata.Illustrators.Select(i => i.Name).ToList();
        if (editorNames.Count > 0 || illustratorNames.Count > 0)
        {
            var lines = new List<string>();
            if (editorNames.Count > 0)
                lines.Add($"Edited by {Encode(string.Join(", ", editorNames))}");
            if (illustratorNames.Count > 0)
                lines.Add($"Illustrated by {Encode(string.Join(", ", illustratorNames))}");
            sb.AppendLine($"<p class=\"centered-block\">{string.Join("<br>\n", lines)}</p>");
        }

        if (metadata.Publisher is { } publisher)
            sb.AppendLine($"<p class=\"centered-block\"><strong class=\"contributor-name\">{Encode(publisher.Name)}</strong></p>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the book's imprint page: cover thumbnail and the contributor/publisher/ISBN
    /// details usually found on such a page near the top, with the copyright statement and
    /// disclaimer at the bottom — the file/spine slot is still "copyright.ebhtml" (renaming it
    /// would touch every existing project), but its content is the fuller imprint-page
    /// convention rather than just a bare copyright line.
    /// </summary>
    public string GenerateCopyrightPage(BookMetadata metadata)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(metadata.CoverImagePath))
            sb.AppendLine($"<p><img src=\"../{Encode(metadata.CoverImagePath)}\" alt=\"Cover\"></p>");

        var titleLines = new List<string> { $"<strong>{Encode(metadata.Title)}</strong>" };
        if (!string.IsNullOrWhiteSpace(metadata.Subtitle))
            titleLines.Add($"<em>{Encode(metadata.Subtitle)}</em>");

        var authorNames = metadata.Authors.Select(a => a.Name).ToList();
        if (authorNames.Count > 0)
            titleLines.Add($"<span class=\"author-name\">By <strong class=\"contributor-name\">{Encode(string.Join(", ", authorNames))}</strong></span>");

        var illustratorNames = metadata.Illustrators.Select(i => i.Name).ToList();
        if (illustratorNames.Count > 0)
            titleLines.Add($"Illustrated by <strong class=\"contributor-name\">{Encode(string.Join(", ", illustratorNames))}</strong>");

        var editorNames = metadata.Editors.Select(e => e.Name).ToList();
        if (editorNames.Count > 0)
            titleLines.Add($"Edited by <strong class=\"contributor-name\">{Encode(string.Join(", ", editorNames))}</strong>");

        sb.AppendLine($"<p class=\"centered-block\">{string.Join("<br>\n", titleLines)}</p>");

        if (metadata.Publisher is { LogoPath: { Length: > 0 } logoPath } publisherWithLogo)
            sb.AppendLine(BuildPublisherLogoFigure(publisherWithLogo, logoPath));

        var publisherLines = new List<string>();
        if (metadata.Publisher is { } publisher)
            publisherLines.Add($"Published by <strong class=\"contributor-name\">{Encode(publisher.Name)}</strong>");
        if (!string.IsNullOrWhiteSpace(metadata.Isbn13))
            publisherLines.Add($"ISBN-13: {Encode(metadata.Isbn13)}");
        if (!string.IsNullOrWhiteSpace(metadata.Isbn10))
            publisherLines.Add($"ISBN-10: {Encode(metadata.Isbn10)}");
        if (metadata.PublicationDate is { } publicationDate)
            publisherLines.Add($"Published {publicationDate:yyyy-MM-dd}");
        if (publisherLines.Count > 0)
            sb.AppendLine($"<p>{string.Join("<br>\n", publisherLines)}</p>");

        var year = metadata.CopyrightYear ?? metadata.PublicationDate?.Year;
        var holder = string.IsNullOrWhiteSpace(metadata.CopyrightHolder)
            ? string.Join(", ", authorNames)
            : metadata.CopyrightHolder;
        sb.AppendLine($"<p>Copyright © {year} {Encode(holder)}</p>");
        sb.AppendLine(WrapParagraphs(metadata.CopyrightDisclaimer));

        return sb.ToString();
    }

    public string GenerateTocPage(IReadOnlyList<SpineItem> spine)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Table of Contents</h1>");
        sb.AppendLine("<ul>");

        foreach (var item in spine.OrderBy(i => i.Order))
        {
            if (item.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal))
                continue;

            var label = item is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {item.ResolvedNumber}: {item.Title}"
                : item.Title ?? item.RelativePath;

            sb.AppendLine($"<li><a href=\"{Encode(item.RelativePath)}\">{Encode(label)}</a></li>");
        }

        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    public string GenerateAboutAuthorPage(BookMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>About the Author</h1>");

        if (metadata.AboutAuthor?.PhotoPath is { Length: > 0 } photoPath)
        {
            var caption = metadata.AboutAuthor.PhotoCaption is { Length: > 0 } explicitCaption
                ? explicitCaption
                : metadata.Authors.FirstOrDefault()?.Name;

            sb.AppendLine(caption is { Length: > 0 }
                ? $"<figure><img src=\"../{Encode(photoPath)}\" alt=\"Author photo\"><figcaption class=\"caption\">{Encode(caption)}</figcaption></figure>"
                : $"<figure><img src=\"../{Encode(photoPath)}\" alt=\"Author photo\"></figure>");
        }

        if (!string.IsNullOrWhiteSpace(metadata.AboutAuthor?.Bio))
            sb.AppendLine(WrapParagraphs(metadata.AboutAuthor.Bio));

        var socialLinks = metadata.AboutAuthor?.SocialLinks ?? [];
        if (socialLinks.Count > 0)
        {
            sb.AppendLine("<h2>Connect</h2>");
            sb.AppendLine("<ul>");
            foreach (var link in socialLinks)
                sb.AppendLine($"<li><a href=\"{Encode(link.Url)}\">{Encode(link.Platform)}</a></li>");
            sb.AppendLine("</ul>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the back-matter "Index" page's body from every "Mark as Index Entry" occurrence
    /// in the book (see IndexEntryScanner) — grouped by term (case-insensitive; the visible
    /// label uses whichever casing that term's earliest occurrence in book order used) and
    /// alphabetized, each term linking once per chapter/page it occurs in (a "page" has no
    /// meaning in a reflowable format, so chapter is the natural non-PDF granularity — see
    /// HtmlToPdfRenderer for the real, distinct-per-occurrence page-number resolution PDF export
    /// gets instead). Called only by an explicit "Generate/Regenerate Index" command, never as a
    /// side effect of every edit — scanning every chapter's body is too expensive for that.
    /// </summary>
    public string GenerateIndexPage(IReadOnlyList<IndexOccurrence> occurrences)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Index</h1>");

        if (occurrences.Count == 0)
        {
            sb.AppendLine("<p><em>No index entries have been marked yet. Select some text and use \"Mark as Index Entry…\" to add one, then regenerate this page.</em></p>");
            return sb.ToString();
        }

        sb.AppendLine("<ul class=\"index-list\">");
        foreach (var termGroup in occurrences.GroupBy(o => o.Term, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var displayTerm = termGroup.First().Term;
            var perChapterEntries = termGroup
                .GroupBy(o => o.Item.RelativePath)
                .Select(chapterGroup => chapterGroup.First())
                .OrderBy(o => o.Item.Order);

            var links = perChapterEntries.Select(o =>
                $"<a href=\"{Encode(o.Item.RelativePath)}#{Encode(o.MarkerId)}\">{Encode(o.Item.DisplayTitle)}</a>");

            sb.AppendLine($"<li>{Encode(displayTerm)} — {string.Join(", ", links)}</li>");
        }
        sb.AppendLine("</ul>");

        return sb.ToString();
    }

    public void RegenerateAllGeneratedPages(EbookProject project)
    {
        File.WriteAllText(
            Path.Combine(project.FrontMatterDir, ProjectPaths.TitlePageFileName),
            GenerateTitlePage(project.Metadata));

        File.WriteAllText(
            Path.Combine(project.FrontMatterDir, ProjectPaths.CopyrightPageFileName),
            GenerateCopyrightPage(project.Metadata));

        File.WriteAllText(
            Path.Combine(project.FrontMatterDir, ProjectPaths.TocPageFileName),
            GenerateTocPage(project.Spine));

        File.WriteAllText(
            Path.Combine(project.BackMatterDir, ProjectPaths.AboutAuthorPageFileName),
            GenerateAboutAuthorPage(project.Metadata));
    }

    /// <summary>
    /// Splits free-form user text (e.g. an author bio or the copyright disclaimer) into
    /// HTML paragraphs on blank lines, preserving single line breaks within a paragraph as
    /// &lt;br&gt;. Each paragraph's text is HTML-encoded before the &lt;br&gt; tags are
    /// reinserted, so encoding never touches the tags themselves.
    /// </summary>
    private static string WrapParagraphs(string text)
    {
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var lines = paragraph.Split('\n').Select(Encode);
            sb.AppendLine($"<p>{string.Join("<br>\n", lines)}</p>");
        }

        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// Builds the imprint page's publisher-logo block: the logo image centered on its own
    /// line with the publisher name as a centered caption below it. When the publisher has a
    /// URL, both the image and the caption link to it.
    /// </summary>
    private static string BuildPublisherLogoFigure(PublisherInfo publisher, string logoPath)
    {
        var img = $"<img src=\"../{Encode(logoPath)}\" alt=\"{Encode(publisher.Name)}\">";
        var caption = $"<span class=\"caption\">{Encode(publisher.Name)}</span>";

        if (!string.IsNullOrWhiteSpace(publisher.Url))
        {
            var url = Encode(publisher.Url);
            img = $"<a href=\"{url}\">{img}</a>";
            caption = $"<a href=\"{url}\">{caption}</a>";
        }

        return $"<p class=\"centered-block\">{img}<br>\n{caption}</p>";
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}

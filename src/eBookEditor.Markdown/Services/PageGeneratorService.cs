using System.Text;
using eBookEditor.Core.Models;

namespace eBookEditor.Markdown.Services;

public class PageGeneratorService
{
    public string GenerateTitlePage(BookMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {metadata.Title}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(metadata.Subtitle))
        {
            sb.AppendLine($"## {metadata.Subtitle}");
            sb.AppendLine();
        }

        var authorNames = metadata.Authors.Select(a => a.Name).ToList();
        if (authorNames.Count > 0)
        {
            sb.AppendLine($"*by {string.Join(", ", authorNames)}*");
            sb.AppendLine();
        }

        var editorNames = metadata.Editors.Select(e => e.Name).ToList();
        if (editorNames.Count > 0)
            sb.AppendLine($"Edited by {string.Join(", ", editorNames)}  ");

        var illustratorNames = metadata.Illustrators.Select(i => i.Name).ToList();
        if (illustratorNames.Count > 0)
            sb.AppendLine($"Illustrated by {string.Join(", ", illustratorNames)}  ");

        if (metadata.Publisher is { } publisher)
        {
            sb.AppendLine();
            sb.AppendLine(publisher.Name);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates the book's imprint page: cover thumbnail and the contributor/publisher/ISBN
    /// details usually found on such a page near the top, with the copyright statement and
    /// disclaimer at the bottom — the file/spine slot is still "copyright.md" (renaming it
    /// would touch every existing project), but its content is the fuller imprint-page
    /// convention rather than just a bare copyright line.
    /// </summary>
    public string GenerateCopyrightPage(BookMetadata metadata)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(metadata.CoverImagePath))
        {
            sb.AppendLine($"![Cover](../{metadata.CoverImagePath})");
            sb.AppendLine();
        }

        sb.AppendLine($"**{metadata.Title}**  ");
        if (!string.IsNullOrWhiteSpace(metadata.Subtitle))
            sb.AppendLine($"*{metadata.Subtitle}*  ");

        var authorNames = metadata.Authors.Select(a => a.Name).ToList();
        if (authorNames.Count > 0)
            sb.AppendLine($"By {string.Join(", ", authorNames)}  ");

        var illustratorNames = metadata.Illustrators.Select(i => i.Name).ToList();
        if (illustratorNames.Count > 0)
            sb.AppendLine($"Illustrated by {string.Join(", ", illustratorNames)}  ");

        var editorNames = metadata.Editors.Select(e => e.Name).ToList();
        if (editorNames.Count > 0)
            sb.AppendLine($"Edited by {string.Join(", ", editorNames)}  ");

        sb.AppendLine();

        if (metadata.Publisher is { } publisher)
            sb.AppendLine($"Published by {publisher.Name}  ");

        if (!string.IsNullOrWhiteSpace(metadata.Isbn13))
            sb.AppendLine($"ISBN-13: {metadata.Isbn13}  ");
        if (!string.IsNullOrWhiteSpace(metadata.Isbn10))
            sb.AppendLine($"ISBN-10: {metadata.Isbn10}  ");
        if (metadata.PublicationDate is { } publicationDate)
            sb.AppendLine($"Published {publicationDate:yyyy-MM-dd}  ");

        sb.AppendLine();
        sb.AppendLine();

        var year = metadata.CopyrightYear ?? metadata.PublicationDate?.Year;
        var holder = string.IsNullOrWhiteSpace(metadata.CopyrightHolder)
            ? string.Join(", ", authorNames)
            : metadata.CopyrightHolder;
        sb.AppendLine($"Copyright © {year} {holder}".TrimEnd());
        sb.AppendLine();
        sb.AppendLine(metadata.CopyrightDisclaimer);

        return sb.ToString();
    }

    public string GenerateTocPage(IReadOnlyList<SpineItem> spine)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Table of Contents");
        sb.AppendLine();

        foreach (var item in spine.OrderBy(i => i.Order))
        {
            if (item.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal))
                continue;

            var label = item is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {item.ResolvedNumber}: {item.Title}"
                : item.Title ?? item.RelativePath;

            sb.AppendLine($"- [{label}]({item.RelativePath})");
        }

        return sb.ToString();
    }

    public string GenerateAboutAuthorPage(BookMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# About the Author");
        sb.AppendLine();

        if (metadata.AboutAuthor?.PhotoPath is { Length: > 0 } photoPath)
        {
            sb.AppendLine($"![Author photo](../{photoPath})");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(metadata.AboutAuthor?.Bio))
        {
            sb.AppendLine(metadata.AboutAuthor.Bio);
            sb.AppendLine();
        }

        var socialLinks = metadata.AboutAuthor?.SocialLinks ?? [];
        if (socialLinks.Count > 0)
        {
            sb.AppendLine("## Connect");
            sb.AppendLine();
            foreach (var link in socialLinks)
                sb.AppendLine($"- [{link.Platform}]({link.Url})");
        }

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
}

using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Pdf.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace eBookEditor.Pdf.Services;

/// <summary>
/// Builds a print-formatted PDF from a project: every chapter (and front/back matter page)
/// starts on its own page, front matter is numbered with lowercase roman numerals and the
/// body with arabic numerals (see FrontMatterAwarePageNumberFooter), and the table of
/// contents links to each chapter with its real page number via QuestPDF's Section/
/// SectionLink support. The imprint (copyright) page is rendered directly from metadata
/// rather than through the generic Markdown renderer, so its copyright statement can be
/// pinned to the bottom of the page — something a reflowable EPUB page can't do, but a fixed
/// PDF page can.
/// </summary>
public class PdfBuilder
{
    private readonly MarkdownToPdfRenderer _renderer = new();
    private readonly ChapterFileService _chapterFileService = new();

    static PdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportResult Build(EbookProject project, string outputPath)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var metadata = project.Metadata;
        var pageSize = PdfPageSizeResolver.Resolve(metadata.PdfPageSize);
        var orderedSpine = project.Spine.OrderBy(i => i.Order).ToList();
        var frontMatterPageCount = orderedSpine.TakeWhile(i => i.Type == SpineItemType.FrontMatter).Count();
        var footer = new FrontMatterAwarePageNumberFooter(frontMatterPageCount);
        var wordCount = 0;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(0.75f, Unit.Inch);
                page.DefaultTextStyle(text => text.FontFamily("Times New Roman").FontSize(11));

                page.Content().Column(column =>
                {
                    var isFirstItem = true;
                    foreach (var item in orderedSpine)
                    {
                        if (!isFirstItem)
                            column.Item().PageBreak();
                        isFirstItem = false;

                        if (item.RelativePath.EndsWith(ProjectPaths.CopyrightPageFileName, StringComparison.Ordinal))
                        {
                            RenderImprintPage(column, project, metadata);
                            continue;
                        }

                        if (item.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal))
                        {
                            RenderTableOfContents(column, orderedSpine);
                            continue;
                        }

                        var (_, body) = _chapterFileService.ReadChapter(project.ResolvePath(item));
                        if (item.Type == SpineItemType.Chapter)
                            wordCount += CountWords(body);

                        _renderer.RenderMarkdownBody(column, body, SectionName(item));
                    }
                });

                page.Footer().Dynamic(footer);
            });
        }).GeneratePdf(outputPath);

        return new PdfExportResult(outputPath, footer.TotalPages, wordCount);
    }

    private static void RenderImprintPage(ColumnDescriptor column, EbookProject project, BookMetadata metadata)
    {
        column.Item().Column(top =>
        {
            var coverPath = string.IsNullOrWhiteSpace(metadata.CoverImagePath)
                ? null
                : Path.Combine(project.DirectoryPath, metadata.CoverImagePath);

            if (coverPath is not null && File.Exists(coverPath))
                top.Item().AlignCenter().Width(120).Image(coverPath).FitWidth();

            top.Item().PaddingTop(12).Text(metadata.Title).Bold().FontSize(14);
            if (!string.IsNullOrWhiteSpace(metadata.Subtitle))
                top.Item().Text(metadata.Subtitle).Italic().FontSize(11);

            var authorNames = metadata.Authors.Select(a => a.Name).ToList();
            if (authorNames.Count > 0)
                top.Item().PaddingTop(6).Text($"By {string.Join(", ", authorNames)}").FontSize(10);

            var illustratorNames = metadata.Illustrators.Select(i => i.Name).ToList();
            if (illustratorNames.Count > 0)
                top.Item().Text($"Illustrated by {string.Join(", ", illustratorNames)}").FontSize(10);

            var editorNames = metadata.Editors.Select(e => e.Name).ToList();
            if (editorNames.Count > 0)
                top.Item().Text($"Edited by {string.Join(", ", editorNames)}").FontSize(10);

            if (metadata.Publisher is { } publisher)
                top.Item().PaddingTop(6).Text($"Published by {publisher.Name}").FontSize(10);
            if (!string.IsNullOrWhiteSpace(metadata.Isbn13))
                top.Item().Text($"ISBN-13: {metadata.Isbn13}").FontSize(10);
            if (!string.IsNullOrWhiteSpace(metadata.Isbn10))
                top.Item().Text($"ISBN-10: {metadata.Isbn10}").FontSize(10);
            if (metadata.PublicationDate is { } publicationDate)
                top.Item().Text($"Published {publicationDate:yyyy-MM-dd}").FontSize(10);
        });

        column.Item().ExtendVertical();

        column.Item().Column(bottom =>
        {
            var year = metadata.CopyrightYear ?? metadata.PublicationDate?.Year;
            var holder = string.IsNullOrWhiteSpace(metadata.CopyrightHolder)
                ? string.Join(", ", metadata.Authors.Select(a => a.Name))
                : metadata.CopyrightHolder;
            bottom.Item().Text($"Copyright © {year} {holder}".TrimEnd()).FontSize(9);
            bottom.Item().PaddingTop(4).Text(metadata.CopyrightDisclaimer).FontSize(8);
        });
    }

    private static void RenderTableOfContents(ColumnDescriptor column, IReadOnlyList<SpineItem> spine)
    {
        column.Item().PaddingBottom(12).Text("Table of Contents").Bold().FontSize(16);

        foreach (var item in spine)
        {
            if (item.RelativePath.EndsWith(ProjectPaths.TocPageFileName, StringComparison.Ordinal))
                continue;

            var label = item is { Type: SpineItemType.Chapter, ResolvedNumber: not null }
                ? $"Chapter {item.ResolvedNumber}: {item.Title}"
                : item.Title ?? item.RelativePath;

            column.Item().PaddingBottom(4).Row(row =>
            {
                row.RelativeItem().Text(text => text.SectionLink(label, SectionName(item)).FontSize(11));
                row.AutoItem().Text(text => text.BeginPageNumberOfSection(SectionName(item)).FontSize(11));
            });
        }
    }

    private static string SectionName(SpineItem item) => $"spine-{item.Id}";

    private static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}

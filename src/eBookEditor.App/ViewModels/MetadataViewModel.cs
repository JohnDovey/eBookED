using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using eBookEditor.Core.Models;
using eBookEditor.Epub.Services;

namespace eBookEditor.App.ViewModels;

/// <summary>
/// Lists (authors, tags, social/store links) are edited as simple delimited text for this
/// first pass rather than dynamic add/remove rows — a reasonable v1 simplification, not a
/// hidden gap: "Name1, Name2" for people/tags, "Platform|Url" one-per-line for links.
/// </summary>
public partial class MetadataViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _authorNames = string.Empty;
    [ObservableProperty] private string _editorNames = string.Empty;
    [ObservableProperty] private string _illustratorNames = string.Empty;
    [ObservableProperty] private string _copyrightHolder = string.Empty;
    [ObservableProperty] private string _copyrightYear = string.Empty;
    [ObservableProperty] private string _publisherName = string.Empty;
    [ObservableProperty] private string _publisherLogoPath = string.Empty;
    [ObservableProperty] private string _coverImagePath = string.Empty;
    [ObservableProperty] private string _publicationDate = string.Empty;
    [ObservableProperty] private string _language = "en";
    [ObservableProperty] private string _genreTags = string.Empty;
    [ObservableProperty] private string _freeTags = string.Empty;
    [ObservableProperty] private string _blurb = string.Empty;
    [ObservableProperty] private string _isbn10 = string.Empty;
    [ObservableProperty] private string _isbn13 = string.Empty;
    [ObservableProperty] private string _authorBio = string.Empty;
    [ObservableProperty] private string _authorPhotoPath = string.Empty;
    [ObservableProperty] private string _socialLinks = string.Empty;
    [ObservableProperty] private string _storeLinks = string.Empty;
    [ObservableProperty] private string _copyrightDisclaimer = BookMetadata.DefaultDisclaimerText;
    [ObservableProperty] private string _selectedTemplate = TemplateService.DefaultTemplateName;

    public ObservableCollection<string> AvailableTemplates { get; } = [];

    /// <summary>
    /// Rescans the templates directory and refreshes the picker list. Called each time the
    /// template picker is opened, not cached, so newly-added .css files always show up.
    /// </summary>
    public void RefreshAvailableTemplates(TemplateService templateService)
    {
        var names = templateService.ScanTemplateNames();

        AvailableTemplates.Clear();
        foreach (var name in names)
            AvailableTemplates.Add(name);

        if (!AvailableTemplates.Contains(SelectedTemplate))
            SelectedTemplate = AvailableTemplates.Contains(TemplateService.DefaultTemplateName)
                ? TemplateService.DefaultTemplateName
                : AvailableTemplates.FirstOrDefault() ?? TemplateService.DefaultTemplateName;
    }

    public void LoadFrom(BookMetadata metadata)
    {
        Title = metadata.Title;
        Subtitle = metadata.Subtitle ?? "";
        AuthorNames = string.Join(", ", metadata.Authors.Select(a => a.Name));
        EditorNames = string.Join(", ", metadata.Editors.Select(e => e.Name));
        IllustratorNames = string.Join(", ", metadata.Illustrators.Select(i => i.Name));
        CopyrightHolder = metadata.CopyrightHolder;
        CopyrightYear = metadata.CopyrightYear?.ToString() ?? "";
        PublisherName = metadata.Publisher?.Name ?? "";
        PublisherLogoPath = metadata.Publisher?.LogoPath ?? "";
        CoverImagePath = metadata.CoverImagePath ?? "";
        PublicationDate = metadata.PublicationDate?.ToString("yyyy-MM-dd") ?? "";
        Language = metadata.Language;
        GenreTags = string.Join(", ", metadata.GenreTags);
        FreeTags = string.Join(", ", metadata.FreeTags);
        Blurb = metadata.Blurb ?? "";
        Isbn10 = metadata.Isbn10 ?? "";
        Isbn13 = metadata.Isbn13 ?? "";
        AuthorBio = metadata.AboutAuthor?.Bio ?? "";
        AuthorPhotoPath = metadata.AboutAuthor?.PhotoPath ?? "";
        SocialLinks = string.Join("\n", (metadata.AboutAuthor?.SocialLinks ?? []).Select(s => $"{s.Platform}|{s.Url}"));
        StoreLinks = string.Join("\n", metadata.StoreLinks.Select(s => $"{s.Store}|{s.Url}"));
        CopyrightDisclaimer = metadata.CopyrightDisclaimer;
        SelectedTemplate = metadata.SelectedTemplate ?? TemplateService.DefaultTemplateName;
    }

    public BookMetadata ToMetadata()
    {
        var contributors = new List<Contributor>();
        contributors.AddRange(SplitNames(AuthorNames).Select(n => new Contributor(n, ContributorRole.Author)));
        contributors.AddRange(SplitNames(EditorNames).Select(n => new Contributor(n, ContributorRole.Editor)));
        contributors.AddRange(SplitNames(IllustratorNames).Select(n => new Contributor(n, ContributorRole.Illustrator)));

        return new BookMetadata
        {
            Title = Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(Subtitle) ? null : Subtitle.Trim(),
            Contributors = contributors,
            CopyrightHolder = CopyrightHolder.Trim(),
            CopyrightYear = int.TryParse(CopyrightYear, out var year) ? year : null,
            Publisher = string.IsNullOrWhiteSpace(PublisherName)
                ? null
                : new PublisherInfo(PublisherName.Trim(), string.IsNullOrWhiteSpace(PublisherLogoPath) ? null : PublisherLogoPath.Trim()),
            CoverImagePath = string.IsNullOrWhiteSpace(CoverImagePath) ? null : CoverImagePath.Trim(),
            PublicationDate = DateOnly.TryParse(PublicationDate, out var date) ? date : null,
            Language = string.IsNullOrWhiteSpace(Language) ? "en" : Language.Trim(),
            GenreTags = SplitNames(GenreTags),
            FreeTags = SplitNames(FreeTags),
            Blurb = string.IsNullOrWhiteSpace(Blurb) ? null : Blurb,
            Isbn10 = string.IsNullOrWhiteSpace(Isbn10) ? null : Isbn10.Trim(),
            Isbn13 = string.IsNullOrWhiteSpace(Isbn13) ? null : Isbn13.Trim(),
            AboutAuthor = new AboutAuthorInfo
            {
                Bio = AuthorBio,
                PhotoPath = string.IsNullOrWhiteSpace(AuthorPhotoPath) ? null : AuthorPhotoPath.Trim(),
                SocialLinks = ParseLinks(SocialLinks).Select(l => new SocialLink(l.Key, l.Value)).ToList()
            },
            StoreLinks = ParseLinks(StoreLinks)
                .Select(l => new StoreLink(
                    Enum.TryParse<StoreName>(l.Key, ignoreCase: true, out var store) ? store : StoreName.Other,
                    l.Value,
                    Enum.TryParse<StoreName>(l.Key, ignoreCase: true, out _) ? null : l.Key))
                .ToList(),
            CopyrightDisclaimer = string.IsNullOrWhiteSpace(CopyrightDisclaimer)
                ? BookMetadata.DefaultDisclaimerText
                : CopyrightDisclaimer,
            SelectedTemplate = string.IsNullOrWhiteSpace(SelectedTemplate) ? null : SelectedTemplate
        };
    }

    private static List<string> SplitNames(string text) => text
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    private static List<KeyValuePair<string, string>> ParseLinks(string text) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('|', 2))
        .Where(parts => parts.Length == 2)
        .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()))
        .ToList();
}

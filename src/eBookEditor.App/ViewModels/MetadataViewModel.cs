using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eBookEditor.Core.Models;
using eBookEditor.Core.Services;
using eBookEditor.Epub.Services;

namespace eBookEditor.App.ViewModels;

public partial class MetadataViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _copyrightHolder = string.Empty;
    [ObservableProperty] private string _copyrightYear = string.Empty;
    [ObservableProperty] private string _publisherName = string.Empty;
    [ObservableProperty] private string _publisherLogoPath = string.Empty;
    [ObservableProperty] private string _publisherUrl = string.Empty;
    [ObservableProperty] private string _coverImagePath = string.Empty;
    [ObservableProperty] private DateTime? _publicationDate;
    [ObservableProperty] private string _language = "en";
    [ObservableProperty] private string _blurb = string.Empty;
    [ObservableProperty] private string _isbn10 = string.Empty;
    [ObservableProperty] private string _isbn13 = string.Empty;
    [ObservableProperty] private string _authorBio = string.Empty;
    [ObservableProperty] private string _authorPhotoPath = string.Empty;
    [ObservableProperty] private string _authorPhotoCaption = string.Empty;
    [ObservableProperty] private string _copyrightDisclaimer = BookMetadata.DefaultDisclaimerText;
    [ObservableProperty] private string _selectedTemplate = TemplateService.DefaultTemplateName;
    [ObservableProperty] private string _pdfPageSize = PdfPageSizeCatalog.DefaultName;
    [ObservableProperty] private bool _generateListOfFigures;

    public IReadOnlyList<string> AvailablePdfPageSizes { get; } = PdfPageSizeCatalog.All.Select(o => o.Name).ToList();

    public ObservableCollection<ContributorEntry> Authors { get; } = [];
    public ObservableCollection<ContributorEntry> Editors { get; } = [];
    public ObservableCollection<ContributorEntry> Illustrators { get; } = [];
    public ObservableCollection<TagEntry> GenreTags { get; } = [];
    public ObservableCollection<TagEntry> FreeTags { get; } = [];
    public ObservableCollection<SocialLinkEntry> SocialLinks { get; } = [];
    public ObservableCollection<StoreLinkEntry> StoreLinks { get; } = [];

    public ObservableCollection<string> AvailableTemplates { get; } = [];

    [RelayCommand] private void AddAuthor() => Authors.Add(new ContributorEntry());
    [RelayCommand] private void RemoveAuthor(ContributorEntry entry) => Authors.Remove(entry);
    [RelayCommand] private void AddEditor() => Editors.Add(new ContributorEntry());
    [RelayCommand] private void RemoveEditor(ContributorEntry entry) => Editors.Remove(entry);
    [RelayCommand] private void AddIllustrator() => Illustrators.Add(new ContributorEntry());
    [RelayCommand] private void RemoveIllustrator(ContributorEntry entry) => Illustrators.Remove(entry);
    [RelayCommand] private void AddGenreTag() => GenreTags.Add(new TagEntry());
    [RelayCommand] private void RemoveGenreTag(TagEntry entry) => GenreTags.Remove(entry);
    [RelayCommand] private void AddFreeTag() => FreeTags.Add(new TagEntry());
    [RelayCommand] private void RemoveFreeTag(TagEntry entry) => FreeTags.Remove(entry);
    [RelayCommand] private void AddSocialLink() => SocialLinks.Add(new SocialLinkEntry());
    [RelayCommand] private void RemoveSocialLink(SocialLinkEntry entry) => SocialLinks.Remove(entry);
    [RelayCommand] private void AddStoreLink() => StoreLinks.Add(new StoreLinkEntry());
    [RelayCommand] private void RemoveStoreLink(StoreLinkEntry entry) => StoreLinks.Remove(entry);

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

        ReplaceContributors(Authors, metadata.Authors);
        ReplaceContributors(Editors, metadata.Editors);
        ReplaceContributors(Illustrators, metadata.Illustrators);

        CopyrightHolder = metadata.CopyrightHolder;
        CopyrightYear = metadata.CopyrightYear?.ToString() ?? "";
        PublisherName = metadata.Publisher?.Name ?? "";
        PublisherLogoPath = metadata.Publisher?.LogoPath ?? "";
        PublisherUrl = metadata.Publisher?.Url ?? "";
        CoverImagePath = metadata.CoverImagePath ?? "";
        PublicationDate = metadata.PublicationDate?.ToDateTime(TimeOnly.MinValue);
        Language = metadata.Language;

        GenreTags.Clear();
        foreach (var tag in metadata.GenreTags)
            GenreTags.Add(new TagEntry { Value = tag });

        FreeTags.Clear();
        foreach (var tag in metadata.FreeTags)
            FreeTags.Add(new TagEntry { Value = tag });

        Blurb = metadata.Blurb ?? "";
        Isbn10 = metadata.Isbn10 ?? "";
        Isbn13 = metadata.Isbn13 ?? "";
        AuthorBio = metadata.AboutAuthor?.Bio ?? "";
        AuthorPhotoPath = metadata.AboutAuthor?.PhotoPath ?? "";
        AuthorPhotoCaption = metadata.AboutAuthor?.PhotoCaption ?? "";

        SocialLinks.Clear();
        foreach (var link in metadata.AboutAuthor?.SocialLinks ?? [])
            SocialLinks.Add(new SocialLinkEntry { Platform = link.Platform, Url = link.Url });

        StoreLinks.Clear();
        foreach (var link in metadata.StoreLinks)
            StoreLinks.Add(new StoreLinkEntry { Store = link.DisplayLabel ?? link.Store.ToString(), Url = link.Url });

        CopyrightDisclaimer = metadata.CopyrightDisclaimer;
        SelectedTemplate = metadata.SelectedTemplate ?? TemplateService.DefaultTemplateName;
        PdfPageSize = metadata.PdfPageSize;
        GenerateListOfFigures = metadata.GenerateListOfFigures;
    }

    public BookMetadata ToMetadata()
    {
        var contributors = new List<Contributor>();
        contributors.AddRange(ToContributors(Authors, ContributorRole.Author));
        contributors.AddRange(ToContributors(Editors, ContributorRole.Editor));
        contributors.AddRange(ToContributors(Illustrators, ContributorRole.Illustrator));

        return new BookMetadata
        {
            Title = Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(Subtitle) ? null : Subtitle.Trim(),
            Contributors = contributors,
            CopyrightHolder = CopyrightHolder.Trim(),
            CopyrightYear = int.TryParse(CopyrightYear, out var year) ? year : null,
            Publisher = string.IsNullOrWhiteSpace(PublisherName)
                ? null
                : new PublisherInfo(
                    PublisherName.Trim(),
                    string.IsNullOrWhiteSpace(PublisherLogoPath) ? null : PublisherLogoPath.Trim(),
                    string.IsNullOrWhiteSpace(PublisherUrl) ? null : PublisherUrl.Trim()),
            CoverImagePath = string.IsNullOrWhiteSpace(CoverImagePath) ? null : CoverImagePath.Trim(),
            PublicationDate = PublicationDate is { } pubDate ? DateOnly.FromDateTime(pubDate.Date) : null,
            Language = string.IsNullOrWhiteSpace(Language) ? "en" : Language.Trim(),
            GenreTags = GenreTags.Where(t => !string.IsNullOrWhiteSpace(t.Value)).Select(t => t.Value.Trim()).ToList(),
            FreeTags = FreeTags.Where(t => !string.IsNullOrWhiteSpace(t.Value)).Select(t => t.Value.Trim()).ToList(),
            Blurb = string.IsNullOrWhiteSpace(Blurb) ? null : Blurb,
            Isbn10 = string.IsNullOrWhiteSpace(Isbn10) ? null : Isbn10.Trim(),
            Isbn13 = string.IsNullOrWhiteSpace(Isbn13) ? null : Isbn13.Trim(),
            AboutAuthor = new AboutAuthorInfo
            {
                Bio = AuthorBio,
                PhotoPath = string.IsNullOrWhiteSpace(AuthorPhotoPath) ? null : AuthorPhotoPath.Trim(),
                PhotoCaption = string.IsNullOrWhiteSpace(AuthorPhotoCaption) ? null : AuthorPhotoCaption.Trim(),
                SocialLinks = SocialLinks
                    .Where(s => !string.IsNullOrWhiteSpace(s.Platform) && !string.IsNullOrWhiteSpace(s.Url))
                    .Select(s => new SocialLink(s.Platform.Trim(), s.Url.Trim()))
                    .ToList()
            },
            StoreLinks = StoreLinks
                .Where(s => !string.IsNullOrWhiteSpace(s.Url))
                .Select(s => new StoreLink(
                    Enum.TryParse<StoreName>(s.Store, ignoreCase: true, out var store) ? store : StoreName.Other,
                    s.Url.Trim(),
                    Enum.TryParse<StoreName>(s.Store, ignoreCase: true, out _) ? null : s.Store.Trim()))
                .ToList(),
            CopyrightDisclaimer = string.IsNullOrWhiteSpace(CopyrightDisclaimer)
                ? BookMetadata.DefaultDisclaimerText
                : CopyrightDisclaimer,
            SelectedTemplate = string.IsNullOrWhiteSpace(SelectedTemplate) ? null : SelectedTemplate,
            PdfPageSize = string.IsNullOrWhiteSpace(PdfPageSize) ? PdfPageSizeCatalog.DefaultName : PdfPageSize,
            GenerateListOfFigures = GenerateListOfFigures
        };
    }

    private static void ReplaceContributors(ObservableCollection<ContributorEntry> target, IEnumerable<Contributor> source)
    {
        target.Clear();
        foreach (var contributor in source)
            target.Add(new ContributorEntry { FirstName = contributor.FirstName, LastName = contributor.LastName });
    }

    private static IEnumerable<Contributor> ToContributors(IEnumerable<ContributorEntry> entries, ContributorRole role) => entries
        .Where(e => !string.IsNullOrWhiteSpace(e.FirstName) || !string.IsNullOrWhiteSpace(e.LastName))
        .Select(e => new Contributor(e.FirstName.Trim(), e.LastName.Trim(), role));
}

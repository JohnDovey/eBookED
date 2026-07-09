using eBookEditor.App.ViewModels;
using eBookEditor.Core.Models;

namespace eBookEditor.Tests.App;

public class MetadataViewModelTests
{
    [Fact]
    public void AddAndRemoveCommands_ManipulateCollections()
    {
        var vm = new MetadataViewModel();

        vm.AddAuthorCommand.Execute(null);
        vm.AddEditorCommand.Execute(null);
        vm.AddIllustratorCommand.Execute(null);
        vm.AddGenreTagCommand.Execute(null);
        vm.AddFreeTagCommand.Execute(null);
        vm.AddSocialLinkCommand.Execute(null);
        vm.AddStoreLinkCommand.Execute(null);

        Assert.Single(vm.Authors);
        Assert.Single(vm.Editors);
        Assert.Single(vm.Illustrators);
        Assert.Single(vm.GenreTags);
        Assert.Single(vm.FreeTags);
        Assert.Single(vm.SocialLinks);
        Assert.Single(vm.StoreLinks);

        vm.RemoveAuthorCommand.Execute(vm.Authors[0]);
        vm.RemoveEditorCommand.Execute(vm.Editors[0]);
        vm.RemoveIllustratorCommand.Execute(vm.Illustrators[0]);
        vm.RemoveGenreTagCommand.Execute(vm.GenreTags[0]);
        vm.RemoveFreeTagCommand.Execute(vm.FreeTags[0]);
        vm.RemoveSocialLinkCommand.Execute(vm.SocialLinks[0]);
        vm.RemoveStoreLinkCommand.Execute(vm.StoreLinks[0]);

        Assert.Empty(vm.Authors);
        Assert.Empty(vm.Editors);
        Assert.Empty(vm.Illustrators);
        Assert.Empty(vm.GenreTags);
        Assert.Empty(vm.FreeTags);
        Assert.Empty(vm.SocialLinks);
        Assert.Empty(vm.StoreLinks);
    }

    [Fact]
    public void LoadFromThenToMetadata_RoundTripsContributorSortNameAndAllLists()
    {
        var original = new BookMetadata
        {
            Title = "Round Trip",
            Contributors =
            [
                new Contributor("Jane Doe", ContributorRole.Author, "Doe, Jane"),
                new Contributor("Ed Itor", ContributorRole.Editor)
            ],
            GenreTags = ["Fantasy", "Adventure"],
            FreeTags = ["debut"],
            AboutAuthor = new AboutAuthorInfo
            {
                Bio = "Bio text",
                SocialLinks = [new SocialLink("Twitter", "https://twitter.com/janedoe")]
            },
            StoreLinks = [new StoreLink(StoreName.KindleStore, "https://amazon.com/dp/xyz")]
        };

        var vm = new MetadataViewModel();
        vm.LoadFrom(original);

        Assert.Equal("Jane Doe", vm.Authors.Single().Name);
        Assert.Equal("Doe, Jane", vm.Authors.Single().SortName);
        Assert.Equal("Ed Itor", vm.Editors.Single().Name);
        Assert.Equal(["Fantasy", "Adventure"], vm.GenreTags.Select(t => t.Value));

        var roundTripped = vm.ToMetadata();

        Assert.Equal(2, roundTripped.Contributors.Count);
        var author = roundTripped.Authors.Single();
        Assert.Equal("Jane Doe", author.Name);
        Assert.Equal("Doe, Jane", author.SortName);
        Assert.Equal(["Fantasy", "Adventure"], roundTripped.GenreTags);
        Assert.Equal(["debut"], roundTripped.FreeTags);
        Assert.Equal("Twitter", roundTripped.AboutAuthor!.SocialLinks.Single().Platform);
        Assert.Equal(StoreName.KindleStore, roundTripped.StoreLinks.Single().Store);
    }

    [Fact]
    public void ToMetadata_SkipsBlankRows()
    {
        var vm = new MetadataViewModel { Title = "Blank Rows" };
        vm.Authors.Add(new ContributorEntry { Name = "" });
        vm.GenreTags.Add(new TagEntry { Value = "  " });
        vm.SocialLinks.Add(new SocialLinkEntry { Platform = "Twitter", Url = "" });

        var metadata = vm.ToMetadata();

        Assert.Empty(metadata.Contributors);
        Assert.Empty(metadata.GenreTags);
        Assert.Empty(metadata.AboutAuthor!.SocialLinks);
    }
}

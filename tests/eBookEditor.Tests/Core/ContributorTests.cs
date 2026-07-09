using eBookEditor.Core.Models;

namespace eBookEditor.Tests.Core;

public class ContributorTests
{
    [Fact]
    public void Name_JoinsFirstAndLastNameWithSpace()
    {
        var contributor = new Contributor("John", "Dovey", ContributorRole.Author);

        Assert.Equal("John Dovey", contributor.Name);
    }

    [Fact]
    public void SortName_PutsLastNameFirstWhenBothPartsPresent()
    {
        var contributor = new Contributor("John", "Dovey", ContributorRole.Author);

        Assert.Equal("Dovey, John", contributor.SortName);
    }

    [Fact]
    public void NameAndSortName_FallBackToFirstNameOnlyWhenLastNameIsBlank()
    {
        var contributor = new Contributor("Madonna", "", ContributorRole.Author);

        Assert.Equal("Madonna", contributor.Name);
        Assert.Equal("Madonna", contributor.SortName);
    }
}

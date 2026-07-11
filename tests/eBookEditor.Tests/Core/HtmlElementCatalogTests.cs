using eBookEditor.Core.Services;

namespace eBookEditor.Tests.Core;

public class HtmlElementCatalogTests
{
    [Fact]
    public void Elements_AllHaveNonEmptyLabelTagAndPlaceholder()
    {
        foreach (var element in HtmlElementCatalog.Elements)
        {
            Assert.False(string.IsNullOrWhiteSpace(element.Label));
            Assert.False(string.IsNullOrWhiteSpace(element.Tag));
            Assert.False(string.IsNullOrWhiteSpace(element.PlaceholderText));
        }
    }

    [Fact]
    public void Elements_TagsAreLowercaseWithNoMarkupCharacters()
    {
        foreach (var element in HtmlElementCatalog.Elements)
        {
            Assert.Equal(element.Tag, element.Tag.ToLowerInvariant());
            Assert.DoesNotContain('<', element.Tag);
            Assert.DoesNotContain('>', element.Tag);
        }
    }

    [Fact]
    public void Elements_IncludesTheCoreHeadingsAndBlockquote()
    {
        var tags = HtmlElementCatalog.Elements.Select(e => e.Tag).ToList();

        Assert.Contains("h1", tags);
        Assert.Contains("h2", tags);
        Assert.Contains("h3", tags);
        Assert.Contains("h4", tags);
        Assert.Contains("p", tags);
        Assert.Contains("blockquote", tags);
    }
}

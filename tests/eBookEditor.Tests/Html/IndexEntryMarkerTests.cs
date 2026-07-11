using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class IndexEntryMarkerTests
{
    [Fact]
    public void MarkAllOccurrences_WrapsEveryCaseInsensitiveOccurrence()
    {
        const string html = "<p>The captain smiled. Captain Reyes nodded. The word captaincy is different.</p>";

        var result = IndexEntryMarker.MarkAllOccurrences(html, "captain");

        var spanCount = System.Text.RegularExpressions.Regex.Matches(result, "class=\"index-entry\"").Count;
        Assert.Equal(3, spanCount);
        Assert.Contains("data-index-term=\"captain\"", result);
        Assert.Contains(">captain<", result);
        Assert.Contains(">Captain<", result);
        // "captaincy" contains "captain" as a substring — still gets wrapped (a simple
        // substring match, not a whole-word match), leaving the "cy" suffix outside the span.
        Assert.Contains("cy is different", result);
    }

    [Fact]
    public void MarkAllOccurrences_PreservesSurroundingText()
    {
        const string html = "<p>Before captain after.</p>";

        var result = IndexEntryMarker.MarkAllOccurrences(html, "captain");

        Assert.Contains("Before <span", result);
        Assert.Contains("</span> after", result);
    }

    [Fact]
    public void MarkAllOccurrences_SkipsTextAlreadyInsideAnIndexEntry()
    {
        const string html = "<p><span class=\"index-entry\" data-index-term=\"captain\" id=\"idx:captain:0\">captain</span> and captain again.</p>";

        var result = IndexEntryMarker.MarkAllOccurrences(html, "captain");

        var spanCount = System.Text.RegularExpressions.Regex.Matches(result, "class=\"index-entry\"").Count;
        Assert.Equal(2, spanCount);
    }

    [Fact]
    public void MarkAllOccurrences_NoMatch_ReturnsEquivalentHtml()
    {
        const string html = "<p>No matches here.</p>";

        var result = IndexEntryMarker.MarkAllOccurrences(html, "captain");

        Assert.DoesNotContain("index-entry", result);
    }

    [Fact]
    public void MarkAllOccurrences_BlankTerm_ReturnsHtmlUnchanged()
    {
        const string html = "<p>Some text.</p>";

        var result = IndexEntryMarker.MarkAllOccurrences(html, "  ");

        Assert.Equal(html, result);
    }
}

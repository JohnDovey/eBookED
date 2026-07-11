using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class GeneratedByLineTests
{
    [Fact]
    public void Build_IncludesAppNameVersionAndTimestamp()
    {
        var line = GeneratedByLine.Build(new DateTime(2026, 7, 11, 14, 32, 0));

        Assert.StartsWith("Created with eBook Editor ", line);
        Assert.EndsWith("on 2026-07-11 14:32", line);
    }

    [Fact]
    public void InsertBeforeCopyrightStatement_InsertsImmediatelyBeforeTheMarker()
    {
        var html = "<p>Some title stuff.</p>\n<p>Copyright © 2026 Jane Author</p>\n<p>Disclaimer.</p>";

        var result = GeneratedByLine.InsertBeforeCopyrightStatement(html, new DateTime(2026, 7, 11, 14, 32, 0));

        var creditIndex = result.IndexOf("Created with eBook Editor", StringComparison.Ordinal);
        var copyrightIndex = result.IndexOf("Copyright ©", StringComparison.Ordinal);
        Assert.True(creditIndex >= 0 && creditIndex < copyrightIndex,
            "the credit line must appear before the copyright statement");
    }

    [Fact]
    public void InsertBeforeCopyrightStatement_NoMarkerFound_ReturnsHtmlUnchanged()
    {
        // A hand-rewritten imprint page without the auto-generated "Copyright ©" paragraph
        // shape shouldn't have this line guessed into some arbitrary position.
        const string html = "<p>A completely custom imprint page.</p>";

        var result = GeneratedByLine.InsertBeforeCopyrightStatement(html, DateTime.Now);

        Assert.Equal(html, result);
    }
}

using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class GalleryHtmlBuilderTests
{
    private static readonly PdfPageSizeOption SamplePageSize = new("A5", 5.83, 8.27);

    [Fact]
    public void Build_ProducesATableWithTheGalleryClass()
    {
        var images = new List<GalleryImageSelection> { new("a.jpg", 400, 300, "A") };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        Assert.Contains("<table class=\"gallery\">", html);
    }

    [Fact]
    public void Build_ThreeImages_PutsThemAllInOneRow()
    {
        var images = new List<GalleryImageSelection>
        {
            new("a.jpg", 400, 300, "A"),
            new("b.jpg", 400, 300, "B"),
            new("c.jpg", 400, 300, "C"),
        };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<tr>"));
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "<img ").Count);
    }

    [Fact]
    public void Build_FourImages_StartsASecondRowAndPadsTheRemainingCellsEmpty()
    {
        var images = Enumerable.Range(1, 4).Select(i => new GalleryImageSelection($"img{i}.jpg", 400, 300, $"Caption {i}")).ToList();

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<tr>").Count);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, "<img ").Count);
        // The second row has one real image and two empty padding cells.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<td></td>").Count);
    }

    [Fact]
    public void Build_EachImageGetsItsOwnUniqueFigureId()
    {
        var images = new List<GalleryImageSelection>
        {
            new("a.jpg", 400, 300, "A"),
            new("b.jpg", 400, 300, "B"),
        };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        var ids = System.Text.RegularExpressions.Regex.Matches(html, "<figure id=\"(fig:[a-f0-9]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(2, ids.Count);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public void Build_CaptionDefaultsToTheProvidedDefaultCaption()
    {
        var images = new List<GalleryImageSelection> { new("sunset-photo.jpg", 400, 300, "sunset-photo") };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        Assert.Contains("<figcaption class=\"caption\">sunset-photo</figcaption>", html);
    }

    [Fact]
    public void Build_HtmlEncodesFileNameAndCaption()
    {
        var images = new List<GalleryImageSelection> { new("a & b.jpg", 400, 300, "Cats & Dogs") };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        Assert.Contains("Cats &amp; Dogs", html);
        Assert.Contains("a &amp; b.jpg", html);
        Assert.DoesNotContain("Cats & Dogs", html);
    }

    [Fact]
    public void Build_PreservesEachImagesOwnAspectRatioAtTheSharedColumnWidth()
    {
        var columnWidth = GalleryHtmlBuilder.ComputeColumnWidthPx(SamplePageSize);
        // A 2:1 landscape image and a 1:2 portrait image side by side should get very different
        // heights despite sharing the same column width — not stretched to match each other.
        var images = new List<GalleryImageSelection>
        {
            new("landscape.jpg", 800, 400, "Landscape"),
            new("portrait.jpg", 400, 800, "Portrait"),
        };

        var html = GalleryHtmlBuilder.Build(images, SamplePageSize);

        var expectedLandscapeHeight = (int)Math.Round(columnWidth * 0.5);
        var expectedPortraitHeight = (int)Math.Round(columnWidth * 2.0);
        Assert.Contains($"width=\"{columnWidth}\" height=\"{expectedLandscapeHeight}\"", html);
        Assert.Contains($"width=\"{columnWidth}\" height=\"{expectedPortraitHeight}\"", html);
    }

    [Fact]
    public void ComputeColumnWidthPx_IsRoughlyOneThirdOfThePrintableWidth()
    {
        // A5 is 5.83in wide; printable width after the 0.75in-per-side margin is ~4.33in,
        // i.e. ~415px at 96px/in — a third of that (minus per-cell padding) should land
        // comfortably under 150px for A5, not the old fixed/unbounded default.
        var columnWidth = GalleryHtmlBuilder.ComputeColumnWidthPx(SamplePageSize);

        Assert.InRange(columnWidth, 100, 150);
    }
}

using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class ImagePlacementTests
{
    [Theory]
    [InlineData("float:left", ImageAlignment.Left, true)]
    [InlineData("float: left", ImageAlignment.Left, true)]
    [InlineData("float:right", ImageAlignment.Right, true)]
    [InlineData("text-align:left", ImageAlignment.Left, false)]
    [InlineData("text-align:right", ImageAlignment.Right, false)]
    [InlineData("text-align:center", ImageAlignment.Center, false)]
    [InlineData("", ImageAlignment.Center, false)]
    [InlineData(null, ImageAlignment.Center, false)]
    public void Parse_ReadsAlignmentAndFlowFromTheStyleAttribute(string? style, ImageAlignment expectedAlignment, bool expectedFlow)
    {
        var placement = ImagePlacementParser.Parse(style);

        Assert.Equal(expectedAlignment, placement.Alignment);
        Assert.Equal(expectedFlow, placement.Flow);
    }

    [Fact]
    public void ToFigureStyle_RoundTripsThroughParse()
    {
        var left = new ImagePlacement(ImageAlignment.Left, Flow: true);
        var right = new ImagePlacement(ImageAlignment.Right, Flow: false);
        var center = new ImagePlacement(ImageAlignment.Center, Flow: false);

        Assert.Equal(left, ImagePlacementParser.Parse(left.ToFigureStyle()));
        Assert.Equal(right, ImagePlacementParser.Parse(right.ToFigureStyle()));
        Assert.Equal(center, ImagePlacementParser.Parse(center.ToFigureStyle()));
    }
}

using eBookEditor.Core.Services;
using eBookEditor.Html.Services;

namespace eBookEditor.Tests.Html;

public class TemplateShowcaseHtmlTests
{
    [Fact]
    public void Build_IncludesEveryEditorStyleCatalogClass()
    {
        var html = TemplateShowcaseHtml.Build();

        foreach (var style in EditorStyleCatalog.Styles)
            Assert.Contains($"class=\"{style.ClassName}\"", html);
    }

    [Theory]
    [InlineData("<h1>")]
    [InlineData("<h2>")]
    [InlineData("<h3>")]
    [InlineData("<h4>")]
    [InlineData("<blockquote>")]
    [InlineData("<ul>")]
    [InlineData("<ol>")]
    [InlineData("<table>")]
    [InlineData("<th>")]
    [InlineData("<td>")]
    [InlineData("<img")]
    [InlineData("<hr>")]
    [InlineData("class=\"footnote-ref\"")]
    [InlineData("class=\"footnotes\"")]
    [InlineData("class=\"footnote-back-ref\"")]
    public void Build_IncludesEveryStructuralElementVellumSerifTargets(string expected)
    {
        var html = TemplateShowcaseHtml.Build();

        Assert.Contains(expected, html);
    }
}

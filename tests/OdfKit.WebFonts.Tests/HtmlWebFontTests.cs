using OdfKit.Extensions.Html.WebFonts;

namespace OdfKit.WebFonts.Tests;

public sealed class HtmlWebFontTests
{
    [Fact]
    public void AddStylesheetLink_InsertsEncodedLinkInHead()
    {
        string result = OdfWebFontRequirementCollector.AddStylesheetLink(
            "<html><head><title>x</title></head><body></body></html>",
            "/_odf-fonts/webfonts.css?x=1&y=2");

        Assert.Contains(
            "<link rel=\"stylesheet\" href=\"/_odf-fonts/webfonts.css?x=1&amp;y=2\" /></head>",
            result,
            StringComparison.Ordinal);
    }
}

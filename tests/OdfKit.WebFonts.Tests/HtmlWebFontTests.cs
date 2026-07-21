using OdfKit.Extensions.Html.WebFonts;

namespace OdfKit.WebFonts.Tests;

public sealed class HtmlWebFontTests
{
    [Fact]
    public async Task CollectSupportedAsync_PartitionsMixedTextByCoverageAndRoutePriority()
    {
        using OdfKit.Text.TextDocument document = OdfKit.Text.TextDocument.Create();
        document.AddParagraph("𠆩󠄀一二三丨ㄩ幹𰀀");
        var extB = CreateRoute("ext-b", "Ext B");
        var plane3 = CreateRoute("plane-3", "Plane 3");

        IReadOnlyList<WebFontSubsetRequest> requests = await OdfWebFontRequirementCollector
            .CollectSupportedAsync(
                document,
                [extB, plane3],
                new RouteCoverageFilter(),
                TestContext.Current.CancellationToken);

        Assert.Equal(2, requests.Count);
        Assert.Equal("𠆩󠄀", Assert.Single(requests[0].Sequences).Text);
        Assert.Equal("𰀀", Assert.Single(requests[1].Sequences).Text);
        Assert.DoesNotContain(
            requests.SelectMany(request => request.Sequences),
            sequence => sequence.Text.Contains("一", StringComparison.Ordinal)
                || sequence.Text.Contains("幹", StringComparison.Ordinal));
    }

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

    private static OdfWebFontSourceRoute CreateRoute(string sourceId, string family)
        => new(
            new WebFontFaceIdentity
            {
                FontSourceId = sourceId,
                SourceSha256 = new string('a', 64)
            },
            "test",
            family,
            [WebFontFormat.Woff2]);

    private sealed class RouteCoverageFilter : IWebFontTextCoverageFilter
    {
        public Task<IReadOnlyList<WebFontTextSequence>> FilterSupportedSequencesAsync(
            WebFontFaceIdentity face,
            IReadOnlyList<WebFontTextSequence> sequences,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<WebFontTextSequence> supported = sequences.Where(sequence =>
                face.FontSourceId == "ext-b"
                    ? sequence.UnicodeScalars[0] is >= 0x20000 and <= 0x2FFFF
                    : sequence.UnicodeScalars[0] is >= 0x30000 and <= 0x3FFFF).ToArray();
            return Task.FromResult(supported);
        }
    }
}

using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontSequenceCoverageTests
{
    [Fact]
    public void Filter_PreservesSupportedIvsAsOneCluster()
    {
        IReadOnlyList<WebFontTextSequence> result = WebFontSequenceCoverage.Filter(
            [WebFontTextSequence.Create("一󠄀二")],
            scalar => scalar is 0x4E00 or 0x4E8C,
            sequence => sequence.Equals(new UnicodeVariationSequence(0x4E00, 0xE0100)),
            TestContext.Current.CancellationToken);

        Assert.Equal("一󠄀二", Assert.Single(result).Text);
    }

    [Fact]
    public void Filter_DropsEntireIvsWhenVariationIsUnsupported()
    {
        IReadOnlyList<WebFontTextSequence> result = WebFontSequenceCoverage.Filter(
            [WebFontTextSequence.Create("一󠄀二")],
            scalar => scalar is 0x4E00 or 0x4E8C,
            _ => false,
            TestContext.Current.CancellationToken);

        Assert.Equal("二", Assert.Single(result).Text);
    }

    [Fact]
    public void Filter_PreservesJoinerInsideSupportedGraphemeCluster()
    {
        const string family = "👨‍👩‍👧";
        IReadOnlyList<WebFontTextSequence> result = WebFontSequenceCoverage.Filter(
            [WebFontTextSequence.Create(family)],
            scalar => scalar is 0x1F468 or 0x1F469 or 0x1F467,
            _ => false,
            TestContext.Current.CancellationToken);

        Assert.Equal(family, Assert.Single(result).Text);
    }

    [Fact]
    public void Filter_DropsDefaultIgnorablesWithoutARequestedGlyph()
    {
        IReadOnlyList<WebFontTextSequence> result = WebFontSequenceCoverage.Filter(
            [WebFontTextSequence.Create("\u200D\u2060")],
            _ => true,
            _ => true,
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }
}

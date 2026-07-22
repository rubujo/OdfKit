using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class SfntFontTests
{
    [Fact]
    public void RuntimeCapabilitiesKeepUnsupportedLayoutAndIftClaimsClosed()
    {
        Assert.False(WebFontRuntimeCapabilities.IsAatLayoutSupported);
        Assert.False(WebFontRuntimeCapabilities.IsGraphiteLayoutSupported);
        Assert.False(WebFontRuntimeCapabilities.IsIncrementalFontTransferSupported);
    }

    [Theory]
    [InlineData(0x0627)]
    [InlineData(0x0995)]
    [InlineData(0x0E01)]
    [InlineData(0x0E81)]
    [InlineData(0x1780)]
    public void IsComplexShapingScalarRecognizesSupportedScriptRanges(int scalar)
    {
        Assert.True(SfntFont.IsComplexShapingScalar(scalar));
    }

    [Theory]
    [InlineData(0x0041)]
    [InlineData(0x4E00)]
    public void IsComplexShapingScalarRejectsSimpleScriptRanges(int scalar)
    {
        Assert.False(SfntFont.IsComplexShapingScalar(scalar));
    }

    [Theory]
    [InlineData("morx")]
    [InlineData("mort")]
    [InlineData("kerx")]
    [InlineData("Silf")]
    [InlineData("Glat")]
    [InlineData("Gloc")]
    [InlineData("Feat")]
    [InlineData("Sill")]
    public void IsRejectedLayoutTableRejectsAatAndGraphiteTables(string tag)
    {
        Assert.True(SfntFont.IsRejectedLayoutTable(tag));
    }

    [Theory]
    [InlineData("GDEF")]
    [InlineData("GPOS")]
    [InlineData("GSUB")]
    public void IsRejectedLayoutTableAcceptsOpenTypeLayoutTables(string tag)
    {
        Assert.False(SfntFont.IsRejectedLayoutTable(tag));
    }
}

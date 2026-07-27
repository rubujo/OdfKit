using System.IO.Compression;
using System.Text;
using OdfKit.WebFonts.Build;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class WebFontDeliveryOptimizationTests
{
    [Fact]
    public void UnicodeRangesMergeOnlyAdjacentScalars()
    {
        IReadOnlyList<string> ranges = UnicodeRangeFormatter.Create(
            [0x41, 0x42, 0x43, 0x45, 0x201A9]);

        Assert.Equal(["U+41-43", "U+45", "U+201A9"], ranges);
    }

    [Fact]
    public void UnicodeRangesCompactLargeContiguousCorpusToOneDescriptor()
    {
        IReadOnlyList<string> ranges = UnicodeRangeFormatter.Create(
            Enumerable.Range(0x4E00, 20_000));

        Assert.Equal(["U+4E00-9C1F"], ranges);
    }

    [Fact]
    public void StableSlicesKeepVariationSequenceWithItsBaseScalar()
    {
        IReadOnlyList<WebFontTextSequence> sequences = WebFontAssetBuilder.SelectUniqueSequences(
            "A邉󠄐𠀀",
            16);
        var options = new WebFontBuildOptions { UnicodeRangeSliceSize = 256 };

        IReadOnlyList<IReadOnlyList<WebFontTextSequence>> slices = WebFontAssetBuilder.CreateSlices(
            sequences,
            options);

        Assert.Equal(3, slices.Count);
        Assert.Contains(
            slices.SelectMany(slice => slice),
            sequence => sequence.UnicodeScalars.SequenceEqual([0x9089, 0xE0110]));
    }

    [Fact]
    public void CssEmitsConfiguredDisplayAndFallbackMetrics()
    {
        var manifest = new WebFontManifest
        {
            ProfileId = "test-v1",
            Assets =
            [
                new WebFontAsset
                {
                    FileName = "test.woff2",
                    Sha256 = new string('a', 64),
                    ByteLength = 100,
                    Format = WebFontFormat.Woff2,
                    FontFamily = "Test Face",
                    UnicodeRanges = ["U+4E00-4EFF"]
                }
            ]
        };
        var options = new WebFontBuildOptions
        {
            FontDisplay = WebFontDisplayMode.Optional,
            FallbackMetrics = new WebFontFallbackMetrics
            {
                FontFamily = "Test Fallback",
                LocalFontName = "Arial",
                SizeAdjustPercentage = 98.5,
                AscentOverridePercentage = 90,
                DescentOverridePercentage = 20,
                LineGapOverridePercentage = 0
            }
        };

        string css = WebFontAssetBuilder.CreateCss(manifest, options);

        Assert.Contains("font-display: optional;", css, StringComparison.Ordinal);
        Assert.Contains("src: local('Arial');", css, StringComparison.Ordinal);
        Assert.Contains("size-adjust: 98.5%;", css, StringComparison.Ordinal);
        Assert.Contains("line-gap-override: 0%;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void WoffZlibCompressionRoundTripsAndReducesRepetitiveData()
    {
        byte[] source = System.Text.Encoding.ASCII.GetBytes(new string('A', 16 * 1024));

        byte[] compressed = WebFontWriters.CompressZlib(source);
        using var input = new MemoryStream(compressed, writable: false);
        using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);

        Assert.True(compressed.Length < source.Length);
        Assert.Equal(source, output.ToArray());
    }
}

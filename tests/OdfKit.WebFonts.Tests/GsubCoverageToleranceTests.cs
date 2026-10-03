using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

/// <summary>
/// 真實的舊版字型常在 GSUB coverage 尾端放無效的字形編號（Windows 的標楷體 kaiu.ttf 在第一個 lookup 的
/// coverage 尾端有 0xFFFF），瀏覽器照常載入。閉包計算不能因此拒絕整個字型；但出現在中間或排序錯誤仍是毀損。
/// </summary>
public sealed class GsubCoverageToleranceTests
{
    /// <summary>
    /// 尾端的無效項目保留項目位置但永遠不會觸發替代：其餘項目的替代照常運作。
    /// </summary>
    [Fact]
    public void TrailingOutOfRangeCoverageEntryIsIgnored()
    {
        byte[] table = CreateSingleSubstitution(coverage: [1, 2, 0xFFFF], substitutes: [5, 6, 7]);

        var fromFirst = new HashSet<ushort> { 0, 1 };
        GsubGlyphClosure.Add(table, fromFirst, glyphCount: 10, new GsubGlyphClosure.Budget(1_000_000, CancellationToken.None));
        Assert.Contains((ushort)5, fromFirst);
        Assert.DoesNotContain((ushort)6, fromFirst);
        Assert.DoesNotContain((ushort)7, fromFirst);

        var fromSecond = new HashSet<ushort> { 0, 2 };
        GsubGlyphClosure.Add(table, fromSecond, glyphCount: 10, new GsubGlyphClosure.Budget(1_000_000, CancellationToken.None));
        Assert.Contains((ushort)6, fromSecond);
        Assert.DoesNotContain((ushort)7, fromSecond);
    }

    /// <summary>
    /// 無效項目出現在中間，或有效項目沒有遞增，仍視為毀損並拒絕。
    /// </summary>
    [Theory]
    [InlineData(new int[] { 1, 0xFFFF, 3 })]
    [InlineData(new int[] { 3, 2, 4 })]
    public void CorruptCoverageIsStillRejected(int[] coverage)
    {
        byte[] table = CreateSingleSubstitution(
            coverage.Select(glyph => (ushort)glyph).ToArray(),
            substitutes: [5, 6, 7]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => GsubGlyphClosure.Add(
                table,
                new HashSet<ushort> { 0, 1 },
                glyphCount: 10,
                new GsubGlyphClosure.Budget(1_000_000, CancellationToken.None)));
        Assert.Contains("GSUB-coverageGlyph", exception.Message, StringComparison.Ordinal);
    }

    // 單一 lookup、單一 SingleSubstFormat2 subtable（替代字形陣列與 coverage 一一對應）、coverage format 1。
    private static byte[] CreateSingleSubstitution(ushort[] coverage, ushort[] substitutes)
    {
        var output = new List<byte>();

        void WriteUInt16(int value)
        {
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }

        // GSUB header：version 1.0，ScriptList／FeatureList 指向空表，LookupList 於 10。
        WriteUInt16(1);
        WriteUInt16(0);
        WriteUInt16(10);
        WriteUInt16(10);
        WriteUInt16(10);

        // LookupList：1 個 lookup，位於 offset 4。
        WriteUInt16(1);
        WriteUInt16(4);

        // Lookup：type 1、flag 0、1 個 subtable（offset 8）。
        WriteUInt16(1);
        WriteUInt16(0);
        WriteUInt16(1);
        WriteUInt16(8);

        // SingleSubstFormat2：coverage 位於標頭 6 位元組與替代陣列之後。
        WriteUInt16(2);
        WriteUInt16(6 + (substitutes.Length * 2));
        WriteUInt16(substitutes.Length);
        foreach (ushort glyph in substitutes)
        {
            WriteUInt16(glyph);
        }

        // CoverageFormat1。
        WriteUInt16(1);
        WriteUInt16(coverage.Length);
        foreach (ushort glyph in coverage)
        {
            WriteUInt16(glyph);
        }

        return output.ToArray();
    }
}

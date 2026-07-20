using System.Buffers.Binary;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

/// <summary>
/// WOFF 與 WOFF2 標頭的 totalSfntSize 改為算術計算，不再為取得長度而完整序列化一次
/// TTF。這些測試釘住「算術結果必須與序列化結果完全相同」，避免日後改動悄悄產生偏差。
/// </summary>
public sealed class WebFontWriterSfntSizeTests
{
    [Fact]
    public void WriteWoffDeclaresExactUncompressedSfntLength()
    {
        SfntSubset subset = CreateSubset();
        byte[] sfnt = WebFontWriters.WriteTrueType(subset);

        byte[] woff = WebFontWriters.WriteWoff(subset);

        Assert.Equal((uint)sfnt.Length, BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(16, 4)));
    }

    [Fact]
    public void WriteWoff2DeclaresExactUncompressedSfntLength()
    {
        SfntSubset subset = CreateSubset();
        byte[] sfnt = WebFontWriters.WriteTrueType(subset);

        byte[] woff2 = WebFontWriters.WriteWoff2(subset);

        Assert.Equal((uint)sfnt.Length, BinaryPrimitives.ReadUInt32BigEndian(woff2.AsSpan(16, 4)));
    }

    /// <summary>
    /// WOFF 解碼會以字母序重建 sfnt，因此宣告的 totalSfntSize 必須能通過往返驗證。
    /// </summary>
    [Fact]
    public void DecodeWoffAcceptsWriterDeclaredSfntSize()
    {
        SfntSubset subset = CreateSubset();
        byte[] woff = WebFontWriters.WriteWoff(subset);

        byte[] decoded = ManagedOpenTypeWebFontVerifier.DecodeWoff(woff, 1024 * 1024);

        Assert.Equal(WebFontWriters.WriteTrueType(subset), decoded);
    }

    /// <summary>
    /// 刻意使用非 4 的倍數長度，讓每個表格都需要對齊填充，以驗證長度計算涵蓋 padding。
    /// </summary>
    private static SfntSubset CreateSubset()
    {
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cmap"] = CreateTable(37),
            ["glyf"] = CreateTable(130),
            ["head"] = CreateTable(54),
            ["loca"] = CreateTable(11),
            ["maxp"] = CreateTable(6),
            ["TEST"] = CreateTable(1)
        };
        return new SfntSubset(0x00010000, tables);
    }

    private static byte[] CreateTable(int length)
    {
        var table = new byte[length];
        for (int index = 0; index < length; index++)
        {
            table[index] = (byte)(index + 1);
        }

        return table;
    }
}

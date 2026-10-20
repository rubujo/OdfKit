using System.Collections.Generic;
using System.IO;
using OdfKit.Export.PdfInternal;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以測試內自行組出的最小 CFF 字型驗證 CID 對照與子程式精簡，不依賴任何字型檔。
/// 組裝方式依 Adobe Technical Note #5176 與 #5177 的資料格式撰寫。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class PdfCffFontTests
{
    private static readonly byte[] Subr = [139, 139, 5, 11];

    [Fact]
    public void CidKeyedFontMapsGlyphsThroughCharset()
    {
        CffFont? font = CffFont.TryParse(BuildFont(cidKeyed: true));

        Assert.NotNull(font);
        Assert.True(font.IsCidKeyed);
        Assert.Equal(3, font.GlyphCount);
        Assert.Equal(0, font.CidOf(0));
        Assert.Equal(100, font.CidOf(1));
        Assert.Equal(200, font.CidOf(2));
    }

    [Fact]
    public void NameKeyedFontUsesGlyphIdAsCid()
    {
        CffFont? font = CffFont.TryParse(BuildFont(cidKeyed: false));

        Assert.NotNull(font);
        Assert.False(font.IsCidKeyed);
        Assert.Equal(2, font.CidOf(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubsetKeepsOnlySubroutinesReachableFromKeptGlyphs(bool cidKeyed)
    {
        CffFont font = CffFont.TryParse(BuildFont(cidKeyed))!;

        byte[]? onlyFirst = font.BuildSubset([1]);
        byte[]? both = font.BuildSubset([1, 2]);
        Assert.NotNull(onlyFirst);
        Assert.NotNull(both);

        // 保留字形 2 會多保留它的 Charstring（6 比 1 位元組，多 5）與全域子程式 1（4 比 1 位元組，多 3）。
        Assert.Equal(8, both.Length - onlyFirst.Length);

        CffFont? reparsed = CffFont.TryParse(onlyFirst);
        Assert.NotNull(reparsed);
        Assert.Equal(3, reparsed.GlyphCount);
        Assert.Equal(cidKeyed, reparsed.IsCidKeyed);
        Assert.Equal(font.CidOf(2), reparsed.CidOf(2));
    }

    [Fact]
    public void GlyphUsingSeacMakesSubsetFallBackToWholeFont()
    {
        CffFont font = CffFont.TryParse(BuildFont(cidKeyed: false, seac: true))!;

        // seac 以標準編碼組合其他字形，元件不在保留集合內，所以不能子集化；呼叫端須整份內嵌。
        Assert.Null(font.BuildSubset([1]));
        Assert.NotNull(font.BuildSubset([2]));
    }

    [Fact]
    public void SubsetOfGarbageOrCff2InputIsRejectedWithoutThrowing()
    {
        Assert.Null(CffFont.TryParse([]));
        Assert.Null(CffFont.TryParse([2, 0, 5, 4, 0, 0]));
        Assert.Null(CffFont.TryParse([1, 0, 4, 1, 255, 255, 255, 255, 255, 255, 255, 255]));
    }

    // 三個字形：0 為 .notdef；1 呼叫區域子程式 0；2 呼叫全域子程式 1。偏移運算元一律用 5 位元組。
    private static byte[] BuildFont(bool cidKeyed, bool seac = false)
    {
        byte[] charStrings = Index(
            [
                [14],
                seac ? [139, 139, 139, 139, 14] : [139, 139, 21, 32, 10, 14],
                [139, 139, 21, 33, 29, 14],
            ]);
        byte[] localSubrs = Index([Subr, Subr]);
        byte[] globalSubrs = Index([Subr, Subr]);
        byte[] charset = [0, 0, 100, 0, 200];
        byte[] fdSelect = [3, 0, 1, 0, 0, 0, 0, 3];
        byte[] header = [1, 0, 4, 1];
        byte[] names = Index([[(byte)'T']]);
        byte[] strings = Index([]);

        byte[] privateDict = PrivateDict(0);
        byte[] privateBlock = Concat(PrivateDict(privateDict.Length), localSubrs);

        // Top DICT 大小固定，先以 0 位移算出版面。
        int topSize = TopDict(cidKeyed, 0, 0, 0, 0, 0, privateDict.Length).Length;
        int offset = header.Length + names.Length + Index([new byte[topSize]]).Length + strings.Length + globalSubrs.Length;
        int charsetOffset = cidKeyed ? offset : 0;
        offset += cidKeyed ? charset.Length : 0;
        int fdSelectOffset = cidKeyed ? offset : 0;
        offset += cidKeyed ? fdSelect.Length : 0;
        int charStringsOffset = offset;
        offset += charStrings.Length;

        byte[] fontDictPlaceholder = FontDict(privateDict.Length, 0);
        int fdArrayOffset = cidKeyed ? offset : 0;
        int fdArraySize = cidKeyed ? Index([fontDictPlaceholder]).Length : 0;
        offset += fdArraySize;
        int privateOffset = offset;

        byte[] top = TopDict(cidKeyed, charsetOffset, charStringsOffset, fdArrayOffset, fdSelectOffset, privateOffset, privateDict.Length);
        byte[] fdArray = cidKeyed ? Index([FontDict(privateDict.Length, privateOffset)]) : [];
        return Concat(header, names, Index([top]), strings, globalSubrs, cidKeyed ? charset : [], cidKeyed ? fdSelect : [], charStrings, fdArray, privateBlock);
    }

    private static byte[] TopDict(bool cidKeyed, int charset, int charStrings, int fdArray, int fdSelect, int privateOffset, int privateSize)
    {
        var bytes = new List<byte>();
        if (cidKeyed)
        {
            bytes.AddRange(Int(391));
            bytes.AddRange(Int(392));
            bytes.AddRange(Int(0));
            bytes.AddRange([12, 30]);
            bytes.AddRange(Int(charset));
            bytes.Add(15);
        }

        bytes.AddRange(Int(charStrings));
        bytes.Add(17);
        if (cidKeyed)
        {
            bytes.AddRange(Int(fdArray));
            bytes.AddRange([12, 36]);
            bytes.AddRange(Int(fdSelect));
            bytes.AddRange([12, 37]);
        }
        else
        {
            bytes.AddRange(Int(privateSize));
            bytes.AddRange(Int(privateOffset));
            bytes.Add(18);
        }

        return [.. bytes];
    }

    private static byte[] FontDict(int privateSize, int privateOffset) => [.. Int(privateSize), .. Int(privateOffset), 18];

    // Private DICT：defaultWidthX（運算子 20）加上 Subrs（運算子 19）；Subrs 位於 DICT 之後。
    private static byte[] PrivateDict(int subrsOffset) => [.. Int(500), 20, .. Int(subrsOffset), 19];

    private static byte[] Int(int value) => [29, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] Index(byte[][] items)
    {
        if (items.Length == 0)
        {
            return [0, 0];
        }

        using var stream = new MemoryStream();
        stream.WriteByte((byte)(items.Length >> 8));
        stream.WriteByte((byte)items.Length);
        stream.WriteByte(2);
        int offset = 1;
        stream.WriteByte((byte)(offset >> 8));
        stream.WriteByte((byte)offset);
        foreach (byte[] item in items)
        {
            offset += item.Length;
            stream.WriteByte((byte)(offset >> 8));
            stream.WriteByte((byte)offset);
        }

        foreach (byte[] item in items)
        {
            stream.Write(item, 0, item.Length);
        }

        return stream.ToArray();
    }

    private static byte[] Concat(params byte[][] parts)
    {
        using var stream = new MemoryStream();
        foreach (byte[] part in parts)
        {
            stream.Write(part, 0, part.Length);
        }

        return stream.ToArray();
    }
}

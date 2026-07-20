using System.Buffers.Binary;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class CmapMappingTests
{
    /// <summary>
    /// 先前 format 4 為每個字元產生獨立 segment，長度欄位為 16-bit，因此超過 8,188 個
    /// BMP 字元的子集必定失敗。Big5 常用字集約 13,053 字，CNS 11643 第 1、2 面約
    /// 13,000 字，皆遠超此界線，而各層設定允許的純量數上限為 65,536。
    /// </summary>
    [Fact]
    public void BuildProducesFormat4ForLargeContiguousBmpSubset()
    {
        var mappings = new SortedDictionary<int, ushort>();
        for (int index = 0; index < 20_000; index++)
        {
            mappings.Add(0x4E00 + index, (ushort)(index + 1));
        }

        byte[] cmap = CmapMapping.Build(mappings, []);
        CmapMapping parsed = CmapMapping.Parse(cmap, 20_001);

        Assert.Equal(20_000, parsed.UnicodeMappings.Count);
        Assert.Equal(1, parsed.UnicodeMappings[0x4E00]);
        Assert.Equal(20_000, parsed.UnicodeMappings[0x4E00 + 19_999]);
        Assert.Contains(EnumerateEncodingRecords(cmap), record => record is (3, 1, _));
    }

    /// <summary>
    /// 連續且 idDelta 相同的碼位必須合併為單一 segment，而非一字一 segment。
    /// </summary>
    [Fact]
    public void BuildCoalescesContiguousFormat4Segments()
    {
        var mappings = new SortedDictionary<int, ushort>();
        for (int index = 0; index < 1_000; index++)
        {
            mappings.Add(0x4E00 + index, (ushort)(index + 1));
        }

        byte[] cmap = CmapMapping.Build(mappings, []);
        int format4Offset = EnumerateEncodingRecords(cmap)
            .Where(record => record is (3, 1, _))
            .Select(record => record.Offset)
            .Single();
        ushort segCountX2 = BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(format4Offset + 6, 2));

        // 1,000 個連續字元應合併為 1 個 segment，加上規格要求的 0xFFFF 終止 segment。
        Assert.Equal(4, segCountX2);
    }

    /// <summary>
    /// 極端稀疏且無法以 16-bit length 表示的字集不得讓子集化失敗；依 OpenType 1.9.1，
    /// format 12 存在時 format 4 僅為相容性選配，此時應省略 format 4。
    /// </summary>
    [Fact]
    public void BuildOmitsFormat4WhenSparseSubsetExceedsSixteenBitLength()
    {
        var mappings = new SortedDictionary<int, ushort>();
        for (int index = 0; index < 12_000; index++)
        {
            // 每隔一個碼位取一個，使相鄰碼位不連續，無法合併 segment。
            mappings.Add(0x1000 + (index * 2), (ushort)(index + 1));
        }

        byte[] cmap = CmapMapping.Build(mappings, []);
        (ushort Platform, ushort Encoding, int Offset)[] records = EnumerateEncodingRecords(cmap).ToArray();
        CmapMapping parsed = CmapMapping.Parse(cmap, 12_001);

        Assert.DoesNotContain(records, record => record is (3, 1, _));
        Assert.Contains(records, record => record is (3, 10, _));
        Assert.Equal(12_000, parsed.UnicodeMappings.Count);
    }

    /// <summary>
    /// OpenType 1.9.1 'cmap'：encoding record 必須先依 platformID、再依 encodingID 排序。
    /// </summary>
    [Fact]
    public void BuildSortsEncodingRecordsByPlatformThenEncoding()
    {
        var mappings = new SortedDictionary<int, ushort> { [0x4E00] = 1 };
        var variations = new[] { CreateVariation(0x4E00, 0xE0100, 1) };

        byte[] cmap = CmapMapping.Build(mappings, variations);
        (ushort Platform, ushort Encoding, int Offset)[] records = EnumerateEncodingRecords(cmap).ToArray();

        Assert.Equal(
            [(0, 3), (0, 5), (3, 1), (3, 10)],
            records.Select(record => (record.Platform, record.Encoding)).ToArray());
    }

    /// <summary>
    /// 誇大的 segCount 先前只對整張 cmap table 做範圍檢查即可通過，讓 segment
    /// 展開迴圈可達數十億次迭代。segCount 必須受 subtable 自身宣告的 length 約束。
    /// </summary>
    [Fact]
    public void ParseRejectsFormat4SegmentCountExceedingDeclaredLength()
    {
        // 兩個 segment 需要 16 + 2 × 8 = 32 位元組，但 length 只宣告 24。
        byte[] cmap = CreateFormat4Cmap(
            declaredLength: 24,
            segments: [(0x0041, 0x0041, 0xFFC0), (0xFFFF, 0xFFFF, 1)]);

        Assert.Throws<InvalidDataException>(() => CmapMapping.Parse(cmap, 16));
    }

    /// <summary>
    /// 規格要求 segment 依 endCode 遞增排列；重疊的 segment 會讓同一碼位被重複展開。
    /// </summary>
    [Fact]
    public void ParseRejectsOverlappingFormat4Segments()
    {
        // 兩個 segment 都刻意只對應到合法字圖，確保測試是因 segment 重疊而失敗，
        // 而非先在字圖範圍檢查上失敗（修正前這份輸入可完整解析成功）。
        byte[] cmap = CreateFormat4Cmap(
            declaredLength: 0,
            segments: [(0x0041, 0x0042, 0xFFC0), (0x0042, 0x0050, 1)]);

        Assert.Throws<InvalidDataException>(() => CmapMapping.Parse(cmap, 4096));
    }

    private static CmapVariation CreateVariation(int baseScalar, int selector, ushort glyphId)
        => new(baseScalar, selector, glyphId, usesDefaultGlyph: false);

    private static IEnumerable<(ushort Platform, ushort Encoding, int Offset)> EnumerateEncodingRecords(byte[] cmap)
    {
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(2, 2));
        for (int index = 0; index < count; index++)
        {
            int record = 4 + (index * 8);
            yield return (
                BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(record, 2)),
                BinaryPrimitives.ReadUInt16BigEndian(cmap.AsSpan(record + 2, 2)),
                (int)BinaryPrimitives.ReadUInt32BigEndian(cmap.AsSpan(record + 4, 4)));
        }
    }

    /// <summary>
    /// 建立僅含單一 format 4 subtable 的 cmap；<paramref name="declaredLength"/> 為 0
    /// 時寫入實際長度，否則寫入指定值以模擬不一致的標頭。
    /// </summary>
    private static byte[] CreateFormat4Cmap(
        int declaredLength,
        IReadOnlyList<(ushort Start, ushort End, ushort Delta)> segments)
    {
        int segCount = segments.Count;
        int subtableLength = 16 + (segCount * 8);
        var cmap = new byte[12 + subtableLength];
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(4, 2), 3);
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(6, 2), 1);
        BinaryPrimitives.WriteUInt32BigEndian(cmap.AsSpan(8, 4), 12);

        int subtable = 12;
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(subtable, 2), 4);
        BinaryPrimitives.WriteUInt16BigEndian(
            cmap.AsSpan(subtable + 2, 2),
            checked((ushort)(declaredLength == 0 ? subtableLength : declaredLength)));
        BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(subtable + 6, 2), checked((ushort)(segCount * 2)));

        int endCodes = subtable + 14;
        int startCodes = endCodes + (segCount * 2) + 2;
        int deltas = startCodes + (segCount * 2);
        for (int index = 0; index < segCount; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(endCodes + (index * 2), 2), segments[index].End);
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(startCodes + (index * 2), 2), segments[index].Start);
            BinaryPrimitives.WriteUInt16BigEndian(cmap.AsSpan(deltas + (index * 2), 2), segments[index].Delta);
        }

        return cmap;
    }
}

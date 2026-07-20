using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

/// <summary>
/// GSUB 閉包的外層 fixed-point 迴圈有次數上限（4096），但那不限制每輪的工作量：
/// lookup、subtable 與 coverage 三層相乘後，單輪即可產生極大的掃描量。取消權杖只能
/// 讓它可中斷，不構成界限，因此另設以 coverage 項目數計費的總量預算。
/// </summary>
public sealed class GsubClosureBudgetTests
{
    /// <summary>
    /// 預算耗盡時必須拒絕，而不是讓掃描跑完。
    /// </summary>
    [Fact]
    public void ExhaustedBudgetIsRejected()
    {
        byte[] table = CreateGsubWithWideCoverage(lookupCount: 32, coverageCount: 256);
        var glyphs = new HashSet<ushort> { 0, 1 };
        var budget = new GsubGlyphClosure.Budget(1_000, CancellationToken.None);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => GsubGlyphClosure.Add(table, glyphs, glyphCount: 1024, budget));

        Assert.Contains("closure-budget", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 預算充足時不得誤傷，且實際消耗必須遠低於預設上限。
    /// </summary>
    [Fact]
    public void SufficientBudgetCompletesAndRecordsConsumption()
    {
        byte[] table = CreateGsubWithWideCoverage(lookupCount: 32, coverageCount: 256);
        var glyphs = new HashSet<ushort> { 0, 1 };
        var budget = new GsubGlyphClosure.Budget(200_000_000, CancellationToken.None);

        GsubGlyphClosure.Add(table, glyphs, glyphCount: 1024, budget);

        Assert.True(budget.Consumed > 0, "預算未被計費，代表計量點沒有生效。");
        Assert.True(
            budget.Consumed < 200_000_000 / 100,
            $"實際消耗 {budget.Consumed:N0} 未保有兩個數量級的餘裕。");
    }

    /// <summary>
    /// 取消權杖必須能中斷閉包掃描。
    /// </summary>
    [Fact]
    public void PreCancelledTokenStopsClosure()
    {
        byte[] table = CreateGsubWithWideCoverage(lookupCount: 32, coverageCount: 256);
        var glyphs = new HashSet<ushort> { 0, 1 };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var budget = new GsubGlyphClosure.Budget(200_000_000, cancellation.Token);

        Assert.ThrowsAny<OperationCanceledException>(
            () => GsubGlyphClosure.Add(table, glyphs, glyphCount: 1024, budget));
    }

    /// <summary>
    /// 建立含指定 lookup 數的 GSUB，每個 lookup 為單一 format 1 single-substitution
    /// subtable，其 coverage 為指定寬度的 format 1 清單。
    /// </summary>
    private static byte[] CreateGsubWithWideCoverage(int lookupCount, int coverageCount)
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

        int lookupListStart = output.Count;
        WriteUInt16(lookupCount);
        int lookupOffsetTable = output.Count;
        for (int index = 0; index < lookupCount; index++)
        {
            WriteUInt16(0);
        }

        var lookupOffsets = new int[lookupCount];
        for (int index = 0; index < lookupCount; index++)
        {
            lookupOffsets[index] = output.Count - lookupListStart;

            // Lookup：type 1、flag 0、1 個 subtable，subtable 緊接其後（offset 8）。
            WriteUInt16(1);
            WriteUInt16(0);
            WriteUInt16(1);
            WriteUInt16(8);

            // SingleSubstFormat1：coverage 於 offset 6，delta 為 1。
            WriteUInt16(1);
            WriteUInt16(6);
            WriteUInt16(1);

            // CoverageFormat1：遞增且互異的字圖清單。
            WriteUInt16(1);
            WriteUInt16(coverageCount);
            for (int glyph = 0; glyph < coverageCount; glyph++)
            {
                WriteUInt16(glyph);
            }
        }

        byte[] table = output.ToArray();
        for (int index = 0; index < lookupCount; index++)
        {
            int position = lookupOffsetTable + (index * 2);
            table[position] = (byte)(lookupOffsets[index] >> 8);
            table[position + 1] = (byte)lookupOffsets[index];
        }

        return table;
    }
}

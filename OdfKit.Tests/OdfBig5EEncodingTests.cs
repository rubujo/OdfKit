using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Core;
using OdfKit.Csv;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 CNS 對照表解析與 Big5E 編碼功能。
/// </summary>
public class OdfBig5EEncodingTests
{
    /// <summary>
    /// 驗證 BMP 與增補平面字元可完成編碼與解碼往返。
    /// </summary>
    [Fact]
    public void Encoding_BmpAndSupplementaryCharacters_RoundTrips()
    {
        OdfBig5EEncoding encoding = CreateEncoding();
        const string original = "一\U0002000B";

        byte[] bytes = encoding.GetBytes(original);
        string decoded = encoding.GetString(bytes);

        Assert.Equal(new byte[] { 0x81, 0x40, 0x81, 0x41 }, bytes);
        Assert.Equal(original, decoded);
    }

    /// <summary>
    /// 驗證 ASCII 字元以單一位元組直接通過。
    /// </summary>
    [Fact]
    public void Encoding_Ascii_PassesThrough()
    {
        OdfBig5EEncoding encoding = CreateEncoding();

        byte[] bytes = encoding.GetBytes("Az0");

        Assert.Equal(new byte[] { 0x41, 0x7A, 0x30 }, bytes);
        Assert.Equal("Az0", encoding.GetString(bytes));
    }

    /// <summary>
    /// 驗證未對應字元、未對應雙位元組與結尾孤立前導位元組均解譯為問號。
    /// </summary>
    [Fact]
    public void Encoding_UnmappedValues_UseQuestionMark()
    {
        OdfBig5EEncoding encoding = CreateEncoding();

        Assert.Equal(new byte[] { 0x3F }, encoding.GetBytes("乙"));
        Assert.Equal("?", encoding.GetString(new byte[] { 0x81, 0x42 }));
        Assert.Equal("?", encoding.GetString(new byte[] { 0x81 }));
        Assert.Equal("??", encoding.GetString(new byte[] { 0x80, 0xFF }));
    }

    /// <summary>
    /// 驗證混合字串的位元組計數與實際編碼長度一致。
    /// </summary>
    [Fact]
    public void GetByteCount_MixedText_MatchesGetBytesLength()
    {
        OdfBig5EEncoding encoding = CreateEncoding();
        const string text = "A一\U0002000B乙\uD800";

        Assert.Equal(encoding.GetByteCount(text), encoding.GetBytes(text).Length);
    }

    /// <summary>
    /// 驗證建立編碼時會拒絕空值、空表與無效碼值。
    /// </summary>
    [Fact]
    public void Create_InvalidMapping_ThrowsExpectedException()
    {
        Assert.Throws<ArgumentNullException>(() => OdfBig5EEncoding.Create(null!));
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(new Dictionary<int, int>()));
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(
            new Dictionary<int, int> { [0x4E00] = 0x0041 }));
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(
            new Dictionary<int, int> { [0x4E00] = 0x8020 }));
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(
            new Dictionary<int, int> { [0x4E00] = 0xFF41 }));
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(
            new Dictionary<int, int> { [0xD800] = 0x8140 }));
    }

    /// <summary>
    /// 驗證 CNS 對照表可解析、略過空行並以共同字碼聯結。
    /// </summary>
    [Fact]
    public void MappingTable_ValidInput_ParsesAndJoins()
    {
        using var sourceReader = new StringReader("1-2121\t3000\n\n3-2144\t2000B\n");
        using var targetReader = new StringReader("1-2121\t8140\n3-2144\t8141\n1-2728\t8E40");

        IReadOnlyDictionary<string, int> source = OdfCns11643MappingTable.Parse(sourceReader);
        IReadOnlyDictionary<string, int> target = OdfCns11643MappingTable.Parse(targetReader);
        IReadOnlyDictionary<int, int> joined = OdfCns11643MappingTable.JoinOnCns(source, target);

        Assert.Equal(0x3000, source["1-2121"]);
        Assert.Equal(0x2000B, source["3-2144"]);
        Assert.Equal(2, joined.Count);
        Assert.Equal(0x8140, joined[0x3000]);
        Assert.Equal(0x8141, joined[0x2000B]);
    }

    /// <summary>
    /// 驗證 CNS 對照表的格式錯誤行會擲出例外。
    /// </summary>
    [Fact]
    public void MappingTable_InvalidLine_ThrowsFormatException()
    {
        using var missingColumn = new StringReader("1-2121");
        using var invalidHex = new StringReader("1-2121\tXYZ");
        using var missingSeparator = new StringReader("12121\t3000");

        Assert.Throws<FormatException>(() => OdfCns11643MappingTable.Parse(missingColumn));
        Assert.Throws<FormatException>(() => OdfCns11643MappingTable.Parse(invalidHex));
        Assert.Throws<FormatException>(() => OdfCns11643MappingTable.Parse(missingSeparator));
    }

    /// <summary>
    /// 驗證 CSV 匯出與匯入可使用同一 Big5E 編碼無損往返。
    /// </summary>
    [Fact]
    public void CsvImportExport_WithBig5EEncoding_RoundTripsMappedText()
    {
        OdfBig5EEncoding encoding = CreateEncoding();
        var options = new OdfCsvOptions { Encoding = encoding, HasHeaders = false };
        using SpreadsheetDocument source = SpreadsheetDocument.Create();
        source.Worksheets.Add("資料").Cells[0, 0].CellValue = "一\U0002000B";
        using var stream = new MemoryStream();

        OdfCsvExporter.ExportToStream(source, stream, options);
        stream.Position = 0;
        using SpreadsheetDocument imported = OdfCsvImporter.ImportFromStream(stream, options);

        Assert.Equal("一\U0002000B", imported.Worksheets[0].Cells[0, 0].CellValue);
    }

    private static OdfBig5EEncoding CreateEncoding() => OdfBig5EEncoding.Create(
        new Dictionary<int, int>
        {
            [0x4E00] = 0x8140,
            [0x2000B] = 0x8141
        });
}

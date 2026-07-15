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
    /// Verifies configured exception fallbacks are honored for unmappable input.
    /// 驗證無法對應的輸入會遵守設定的例外 fallback。
    /// </summary>
    [Fact]
    public void Encoding_ExceptionFallbacks_Throw()
    {
        var encoderEncoding = (Encoding)CreateEncoding().Clone();
        encoderEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        var decoderEncoding = (Encoding)CreateEncoding().Clone();
        decoderEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;

        Assert.Throws<EncoderFallbackException>(() => encoderEncoding.GetBytes("乙"));
        Assert.Throws<DecoderFallbackException>(() => decoderEncoding.GetString(new byte[] { 0x81, 0x42 }));
    }

    /// <summary>
    /// Verifies multi-character replacement fallbacks are emitted without truncation.
    /// 驗證多字元 replacement fallback 會完整輸出而不遭截斷。
    /// </summary>
    [Fact]
    public void Encoding_MultiCharacterReplacementFallbacks_ArePreserved()
    {
        var encoderEncoding = (Encoding)CreateEncoding().Clone();
        encoderEncoding.EncoderFallback = new EncoderReplacementFallback("XY");
        var decoderEncoding = (Encoding)CreateEncoding().Clone();
        decoderEncoding.DecoderFallback = new DecoderReplacementFallback("XY");

        Assert.Equal(new byte[] { 0x58, 0x59 }, encoderEncoding.GetBytes("乙"));
        Assert.Equal("XY", decoderEncoding.GetString(new byte[] { 0x81, 0x42 }));
        Assert.True(encoderEncoding.GetMaxByteCount(1) >= 2);
        Assert.True(decoderEncoding.GetMaxCharCount(2) >= 2);
    }

    /// <summary>
    /// Verifies an unmappable replacement cannot recurse without a bound.
    /// 驗證無法對應的替代字串不會無界遞迴。
    /// </summary>
    [Fact]
    public void Encoding_UnmappableReplacementFallback_Throws()
    {
        var encoding = (Encoding)CreateEncoding().Clone();
        encoding.EncoderFallback = new EncoderReplacementFallback("乙");

        Assert.Throws<EncoderFallbackException>(() => encoding.GetBytes("甲"));
    }

    /// <summary>
    /// Verifies reverse aliases choose a deterministic canonical Unicode scalar.
    /// 驗證反向別名會選擇確定的標準 Unicode 純量值。
    /// </summary>
    [Fact]
    public void Encoding_ReverseAliases_ChooseLowestUnicodeScalar()
    {
        OdfBig5EEncoding encoding = OdfBig5EEncoding.Create(
            new Dictionary<int, int>
            {
                [0x4E01] = 0x8140,
                [0x4E00] = 0x8140
            });

        Assert.Equal("一", encoding.GetString([0x81, 0x40]));
        Assert.Equal([0x81, 0x40], encoding.GetBytes("丁"));
    }

    /// <summary>
    /// Verifies a stateful encoder preserves a surrogate pair split across input blocks.
    /// 驗證具狀態編碼器會保留跨輸入區塊的代理對。
    /// </summary>
    [Fact]
    public void Encoder_SplitSurrogatePair_PreservesState()
    {
        Encoder encoder = CreateEncoding().GetEncoder();
        char[] high = ['\uD840'];
        char[] low = ['\uDC0B'];
        var bytes = new byte[4];

        int firstCount = encoder.GetBytes(high, 0, high.Length, bytes, 0, flush: false);
        int secondCount = encoder.GetBytes(low, 0, low.Length, bytes, firstCount, flush: true);

        Assert.Equal(0, firstCount);
        Assert.Equal(2, secondCount);
        Assert.Equal(new byte[] { 0x81, 0x41 }, bytes[..(firstCount + secondCount)]);
    }

    /// <summary>
    /// Verifies a stateful decoder preserves a Big5E pair split across input blocks.
    /// 驗證具狀態解碼器會保留跨輸入區塊的 Big5E 位元組對。
    /// </summary>
    [Fact]
    public void Decoder_SplitBig5EPair_PreservesState()
    {
        Decoder decoder = CreateEncoding().GetDecoder();
        var chars = new char[4];

        int firstCount = decoder.GetChars(new byte[] { 0x81 }, 0, 1, chars, 0, flush: false);
        int secondCount = decoder.GetChars(new byte[] { 0x40 }, 0, 1, chars, firstCount, flush: true);

        Assert.Equal(0, firstCount);
        Assert.Equal(1, secondCount);
        Assert.Equal("一", new string(chars, 0, firstCount + secondCount));
    }

    /// <summary>
    /// Verifies flushing incomplete state invokes the configured fallback.
    /// 驗證排清未完成狀態時會呼叫設定的 fallback。
    /// </summary>
    [Fact]
    public void StatefulConverters_FlushIncompleteInput_UseFallback()
    {
        Encoder encoder = CreateEncoding().GetEncoder();
        Decoder decoder = CreateEncoding().GetDecoder();
        var bytes = new byte[2];
        var chars = new char[2];

        Assert.Equal(0, encoder.GetBytes(['\uD840'], 0, 1, bytes, 0, flush: false));
        Assert.Equal(1, encoder.GetBytes([], 0, 0, bytes, 0, flush: true));
        Assert.Equal(0x3F, bytes[0]);

        Assert.Equal(0, decoder.GetChars([0x81], 0, 1, chars, 0, flush: false));
        Assert.Equal(1, decoder.GetChars([], 0, 0, chars, 0, flush: true));
        Assert.Equal('?', chars[0]);
    }

    /// <summary>
    /// Verifies block conversion respects a constrained output buffer.
    /// 驗證區塊轉換會遵守受限的輸出緩衝區。
    /// </summary>
    [Fact]
    public void StatefulConverters_Convert_ReportsPartialProgress()
    {
        Encoder encoder = CreateEncoding().GetEncoder();
        char[] input = "一甲".ToCharArray();
        var output = new byte[2];

        encoder.Convert(
            input,
            0,
            input.Length,
            output,
            0,
            output.Length,
            flush: false,
            out int charsUsed,
            out int bytesUsed,
            out bool completed);

        Assert.Equal(1, charsUsed);
        Assert.Equal(2, bytesUsed);
        Assert.False(completed);
        Assert.Equal([0x81, 0x40], output);
    }

    /// <summary>
    /// Verifies an invalid trail byte is reprocessed as the start of the next character.
    /// 驗證無效尾隨位元組會重新作為下一個字元的開頭處理。
    /// </summary>
    [Fact]
    public void Decoder_InvalidTrailByte_DoesNotConsumeFollowingCharacter()
    {
        OdfBig5EEncoding encoding = CreateEncoding();

        Assert.Equal("? A", encoding.GetString(new byte[] { 0x81, 0x20, 0x41 }));
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
        Assert.Throws<ArgumentException>(() => OdfBig5EEncoding.Create(
            new Dictionary<int, int> { [0x41] = 0x8140 }));
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

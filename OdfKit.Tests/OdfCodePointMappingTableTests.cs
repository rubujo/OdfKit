using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfCodePointMappingTable 通用碼位對照表解析與聯結行為之單元測試。
/// </summary>
public class OdfCodePointMappingTableTests
{
    /// <summary>
    /// 驗證可直接解析 Unicode.org 官方對照檔格式（TAB 分隔、0x 前綴、# 註解）。
    /// </summary>
    [Fact]
    public void ParseDelimitedHexParsesUnicodeOrgVendorMappingFormat()
    {
        // 取自 unicode.org Public/MAPPINGS 之 BIG5.TXT 實際格式樣本
        const string sample =
            "# BIG5.TXT vendor mapping sample\n" +
            "#\n" +
            "0xA140\t0x3000\t# IDEOGRAPHIC SPACE\n" +
            "0xA141\t0xFF0C\t# FULLWIDTH COMMA\n" +
            "\n" +
            "0xA440\t0x4E00\t# CJK UNIFIED IDEOGRAPH\n";

        IReadOnlyDictionary<int, int> mapping =
            OdfCodePointMappingTable.ParseDelimitedHex(new StringReader(sample), '\t');

        Assert.Equal(3, mapping.Count);
        Assert.Equal(0x3000, mapping[0xA140]);
        Assert.Equal(0xFF0C, mapping[0xA141]);
        Assert.Equal(0x4E00, mapping[0xA440]);
    }

    /// <summary>
    /// 驗證分號分隔的 UCD 式清單（欄位周圍空白、U+ 前綴、行尾註解）與重複鍵後者覆蓋。
    /// </summary>
    [Fact]
    public void ParseDelimitedHexParsesSemicolonListWithPrefixesAndDuplicates()
    {
        const string sample =
            "U+F0000 ; 20BB7 # PUA to Ext-B\n" +
            "E000;4E00\n" +
            "E000 ; 4E8C # duplicate key, last wins\n";

        IReadOnlyDictionary<int, int> mapping =
            OdfCodePointMappingTable.ParseDelimitedHex(new StringReader(sample), ';');

        Assert.Equal(2, mapping.Count);
        Assert.Equal(0x20BB7, mapping[0xF0000]);
        Assert.Equal(0x4E8C, mapping[0xE000]);
    }

    /// <summary>
    /// 驗證格式不符的資料行擲出 FormatException，null 讀取器擲出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void ParseDelimitedHexValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(null!, '\t'));
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader("only-one-field"), '\t'));
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader("XYZ\t4E00"), '\t'));
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader("E000;;4E00"), ';'));
    }

    /// <summary>
    /// 驗證委派式解析：空行略過、回傳 null 略過、自訂格式可解析。
    /// </summary>
    [Fact]
    public void ParseWithLineParserDelegateSupportsCustomFormats()
    {
        const string sample =
            "MJ000001,F0001,20BB7\n" +
            "\n" +
            "skip-this-line\n" +
            "MJ000002,F0002,4E00\n";

        IReadOnlyDictionary<int, int> mapping = OdfCodePointMappingTable.Parse(
            new StringReader(sample),
            line =>
            {
                string[] fields = line.Split(',');
                if (fields.Length != 3)
                {
                    return null;
                }

                return new KeyValuePair<int, int>(
                    Convert.ToInt32(fields[1], 16),
                    Convert.ToInt32(fields[2], 16));
            });

        Assert.Equal(2, mapping.Count);
        Assert.Equal(0x20BB7, mapping[0xF0001]);
        Assert.Equal(0x4E00, mapping[0xF0002]);

        Assert.Throws<ArgumentNullException>(
            () => OdfCodePointMappingTable.Parse(null!, static _ => null));
        Assert.Throws<ArgumentNullException>(
            () => OdfCodePointMappingTable.Parse(new StringReader(""), null!));
    }

    /// <summary>
    /// 驗證資源預算防線：超長資料行、8 位十六進位溢為負值一律拒絕，例外訊息截斷原始行。
    /// </summary>
    [Fact]
    public void ParseDelimitedHexEnforcesResourceBudgetAndRejectsNegativeHex()
    {
        // 超過 4,096 字元的資料行：資源預算拒絕
        string longLine = new string('A', 5_000) + "\t4E00";
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader(longLine), '\t'));

        string boundaryCrLf = new string(' ', 1_023) + "\r\n4E00\t4E01";
        IReadOnlyDictionary<int, int> boundaryResult = OdfCodePointMappingTable.ParseDelimitedHex(
            new StringReader(boundaryCrLf),
            '\t');
        Assert.Equal(0x4E01, boundaryResult[0x4E00]);

        // 8 位十六進位（FFFFFFFF）溢位為負值：視為無效輸入
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader("FFFFFFFF\t4E00"), '\t'));
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.ParseDelimitedHex(new StringReader("4E00\tFFFFFFFF"), '\t'));

        // 例外訊息中的原始行須截斷並清洗控制字元；以 ANSI ESC 為例，
        // 用 (char)0x1B 建構以避免原始碼內出現裸控制字元或跨層逸出問題。
        char escapeChar = (char)0x1B;
        string sanitized = OdfCodePointMappingTable.FormatLineForMessage(
            "bad" + escapeChar + "[31mline" + new string('x', 200));
        Assert.DoesNotContain(escapeChar, sanitized);
        Assert.True(sanitized.Length <= 65, $"訊息行未截斷：{sanitized.Length}");
        Assert.EndsWith("…", sanitized);

        // 項目數預算檢查本體（直接驗證 internal 檢查點，避免建構兩百萬筆的慢測試）
        OdfCodePointMappingTable.EnsureEntryBudget(OdfCodePointMappingTable.MaxEntryCount);
        Assert.Throws<FormatException>(
            () => OdfCodePointMappingTable.EnsureEntryBudget(OdfCodePointMappingTable.MaxEntryCount + 1));
    }

    /// <summary>
    /// 驗證通用 Join 與 CNS 特化 JoinOnCns 的聯結結果一致。
    /// </summary>
    [Fact]
    public void JoinMatchesJoinOnCnsSemantics()
    {
        var keyToSource = new Dictionary<string, int> { ["1-2121"] = 0x4E00, ["1-2122"] = 0x4E8C };
        var keyToTarget = new Dictionary<string, int> { ["1-2121"] = 0x8E40, ["9-9999"] = 0x8E41 };

        IReadOnlyDictionary<int, int> joined = OdfCodePointMappingTable.Join(keyToSource, keyToTarget);
        IReadOnlyDictionary<int, int> cnsJoined = OdfCns11643MappingTable.JoinOnCns(keyToSource, keyToTarget);

        Assert.Single(joined);
        Assert.Equal(0x8E40, joined[0x4E00]);
        Assert.Equal(joined, cnsJoined);

        Assert.Throws<ArgumentNullException>(
            () => OdfCodePointMappingTable.Join(null!, keyToTarget));
        Assert.Throws<ArgumentNullException>(
            () => OdfCodePointMappingTable.Join(keyToSource, null!));
    }
}

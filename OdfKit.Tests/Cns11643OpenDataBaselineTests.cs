using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以全字庫（CNS 11643 open data）官方對照表為基準的資料驅動驗收測試。
/// 需設定 <c>ODFKIT_CNS11643_MAPPINGTABLES</c> 環境變數指向解壓後的對照表根目錄
/// （含 Unicode/ 與 Big5/ 子目錄，由 eng/Install-Cns11643MappingTables.ps1 準備），
/// 未設定時整批略過；一般 dotnet test 不會自動下載資料。
/// </summary>
public class Cns11643OpenDataBaselineTests
{
    /// <summary>
    /// .NET CP950 與官方 CNS↔Big5 對照表的已知差異白名單（Big5↔Unicode 重複對應歧義字，
    /// CP950 對這兩字選擇不提供編碼）。上游改版若增減差異，測試會失敗以提醒重新釘選。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> KnownCp950Differences = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["1-7641"] = 0x5F5E,
        ["2-4C61"] = 0x7B9A
    };

    /// <summary>
    /// 驗證全字庫全部 CNS↔Unicode 碼位經分段器路由到現行內建規則指定的字型名稱，且分段結果無損。
    /// </summary>
    [Fact]
    public void SegmentText_AllOfficialUnicodeMappings_RouteToExpectedFontsLosslessly()
    {
        string root = GetDataRootOrSkip();
        IReadOnlyDictionary<string, int> cnsToUnicode = LoadAllUnicodeMappings(root);
        Assert.True(cnsToUnicode.Count > 100_000, $"對照表數量異常：{cnsToUnicode.Count}");

        Dictionary<int, StringBuilder> textByPlane = [];
        foreach (int codePoint in cnsToUnicode.Values)
        {
            int plane = codePoint >> 16;
            if (!textByPlane.TryGetValue(plane, out StringBuilder? builder))
            {
                builder = new StringBuilder();
                textByPlane[plane] = builder;
            }

            builder.Append(char.ConvertFromUtf32(codePoint));
        }

        // 現行內建規則（TW-Kai 家族）：Plane 0 維持基礎字型；2 → Ext-B；15 → Plus；
        // Plane 3 全字庫無字面，依規則回退至正規化基礎名稱 TW-Kai-98_1。
        Dictionary<int, string> expectedKaiFontByPlane = new()
        {
            [0] = "TW-Kai",
            [2] = "TW-Kai-Ext-B-98_1",
            [3] = "TW-Kai-98_1",
            [15] = "TW-Kai-Plus-98_1"
        };

        foreach (KeyValuePair<int, StringBuilder> planeText in textByPlane)
        {
            Assert.True(
                expectedKaiFontByPlane.ContainsKey(planeText.Key),
                $"官方對照表出現未預期的 Unicode 平面 {planeText.Key}，請更新內建規則與本測試。");

            string text = planeText.Value.ToString();
            List<(string Text, string FontName)> segments = OdfFontContext.Default.SegmentText(text, "TW-Kai");

            Assert.Single(segments);
            Assert.Equal(expectedKaiFontByPlane[planeText.Key], segments[0].FontName);
            Assert.Equal(text, segments[0].Text);
        }

        // 跨平面混排無損驗證：取樣拼接後分段，串接結果必須等於原文
        var mixed = new StringBuilder();
        int sampleIndex = 0;
        foreach (int codePoint in cnsToUnicode.Values)
        {
            if (sampleIndex++ % 97 == 0)
            {
                mixed.Append(char.ConvertFromUtf32(codePoint));
            }
        }

        string mixedText = mixed.ToString();
        string reassembled = string.Concat(
            OdfFontContext.Default.SegmentText(mixedText, "TW-Kai").Select(static segment => segment.Text));
        Assert.Equal(mixedText, reassembled);
    }

    /// <summary>
    /// 量化 .NET CP950 與官方 CNS↔Big5 對照表的差異：除白名單兩字外必須完全一致，
    /// 且白名單字必須確實差異（防止白名單過期）。
    /// </summary>
    [Fact]
    public void Cp950_MatchesOfficialBig5Table_ExceptKnownDifferences()
    {
        string root = GetDataRootOrSkip();
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Encoding cp950 = Encoding.GetEncoding(950);

        IReadOnlyDictionary<string, int> cnsToUnicode = LoadAllUnicodeMappings(root);
        IReadOnlyDictionary<string, int> cnsToBig5;
        using (var reader = new StreamReader(Path.Combine(root, "Big5", "CNS2BIG5.txt")))
        {
            cnsToBig5 = OdfCns11643MappingTable.Parse(reader);
        }

        List<string> unexpectedDifferences = [];
        int joined = 0;
        foreach (KeyValuePair<string, int> pair in cnsToBig5)
        {
            if (!cnsToUnicode.TryGetValue(pair.Key, out int codePoint))
            {
                continue;
            }

            joined++;
            byte[] actual = cp950.GetBytes(char.ConvertFromUtf32(codePoint));
            bool matches = actual.Length == 2 && ((actual[0] << 8) | actual[1]) == pair.Value;
            bool isKnownDifference = KnownCp950Differences.TryGetValue(pair.Key, out int knownCodePoint) &&
                knownCodePoint == codePoint;

            if (matches == isKnownDifference)
            {
                unexpectedDifferences.Add(
                    $"{pair.Key} U+{codePoint:X4} official={pair.Value:X4} cp950={Convert.ToHexString(actual)}");
            }
        }

        Assert.True(joined > 13_000, $"CNS↔Big5 聯結數量異常：{joined}");
        Assert.True(
            unexpectedDifferences.Count == 0,
            "CP950 與官方 Big5 對照表出現未釘選的差異（或白名單過期）：" + string.Join("；", unexpectedDifferences.Take(10)));
    }

    /// <summary>
    /// 驗證以官方 CNS↔Unicode 與 CNS↔Big5E 對照表建立的 OdfBig5EEncoding 對全部碼位編解碼一致。
    /// </summary>
    [Fact]
    public void Big5EEncoding_OfficialTable_RoundTripsAllCodePoints()
    {
        string root = GetDataRootOrSkip();
        IReadOnlyDictionary<string, int> cnsToUnicode = LoadAllUnicodeMappings(root);
        IReadOnlyDictionary<string, int> cnsToBig5E;
        using (var reader = new StreamReader(Path.Combine(root, "Big5", "CNS2BIG5_Big5E.txt")))
        {
            cnsToBig5E = OdfCns11643MappingTable.Parse(reader);
        }

        IReadOnlyDictionary<int, int> unicodeToBig5E = OdfCns11643MappingTable.JoinOnCns(cnsToUnicode, cnsToBig5E);
        Assert.True(unicodeToBig5E.Count > 3_000, $"Unicode↔Big5E 聯結數量異常：{unicodeToBig5E.Count}");

        OdfBig5EEncoding encoding = OdfBig5EEncoding.Create(unicodeToBig5E);
        foreach (KeyValuePair<int, int> pair in unicodeToBig5E)
        {
            string text = char.ConvertFromUtf32(pair.Key);
            byte[] bytes = encoding.GetBytes(text);
            Assert.Equal(2, bytes.Length);
            Assert.Equal(pair.Value, (bytes[0] << 8) | bytes[1]);

            // 反向表衝突時保留第一筆，因此以「解碼結果的正向值等於原 Big5E 碼」驗證一致性
            string decoded = encoding.GetString(bytes);
            int decodedCodePoint = char.ConvertToUtf32(decoded, 0);
            Assert.Equal(pair.Value, unicodeToBig5E[decodedCodePoint]);
        }
    }

    private static string GetDataRootOrSkip()
    {
        string? root = Environment.GetEnvironmentVariable("ODFKIT_CNS11643_MAPPINGTABLES");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(Path.Combine(root, "Unicode")))
        {
            Assert.Skip("未設定 ODFKIT_CNS11643_MAPPINGTABLES 或目錄不存在，略過全字庫 baseline 驗收。");
        }

        return root!;
    }

    private static IReadOnlyDictionary<string, int> LoadAllUnicodeMappings(string root)
    {
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Unicode"), "CNS2UNICODE_Unicode *.txt"))
        {
            using var reader = new StreamReader(file);
            foreach (KeyValuePair<string, int> pair in OdfCns11643MappingTable.Parse(reader))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}

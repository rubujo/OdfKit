using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfFontSegmenter 的文字字面分段與字型指派之單元測試。
/// </summary>
public class OdfFontSegmenterTests
{
    /// <summary>
    /// 驗證當輸入為空字串或 null 時，分段結果回傳空集合。
    /// </summary>
    [Fact]
    public void SegmentText_WithEmptyOrNull_ReturnsEmptyList()
    {
        var result1 = OdfFontSegmenter.SegmentText(null!, "TW-Kai");
        var result2 = OdfFontSegmenter.SegmentText(string.Empty, "TW-Kai");

        Assert.Empty(result1);
        Assert.Empty(result2);
    }

    /// <summary>
    /// 驗證純 Plane 0 字元（BMP）混合排版時，不進行額外的字型分段。
    /// </summary>
    [Fact]
    public void SegmentText_WithOnlyPlane0_ReturnsSingleSegment()
    {
        string text = "哈囉 World! 這是一般 Unicode 測試字串。";
        string defaultFont = "DFKai-SB";

        var segments = OdfFontSegmenter.SegmentText(text, defaultFont);

        Assert.Single(segments);
        Assert.Equal(text, segments[0].Text);
        Assert.Equal(defaultFont, segments[0].FontName);
    }

    /// <summary>
    /// 驗證混有 Unicode Plane 2（Ext-B）與 Plane 15（PUA 自造字）字元時，正確分割為多個文字片段並指派對應字型。
    /// </summary>
    [Fact]
    public void SegmentText_WithSupplementaryCharacters_SegmentsCorrectly()
    {
        // 𠮷 為 Plane 2 字元 (U+20BB7)
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        // Plane 15 自造字 (U+F0000)
        string plane15Char = char.ConvertFromUtf32(0xF0000);

        string text = "測試" + plane2Char + "中文字" + plane15Char + "結尾";
        string defaultFont = "DFKai-SB";

        var segments = OdfFontSegmenter.SegmentText(text, defaultFont);

        // 應分割為 5 段：
        // 1. "測試" (DFKai-SB)
        // 2. plane2Char (TW-Kai-Ext-B-98_1)
        // 3. "中文字" (DFKai-SB)
        // 4. plane15Char (TW-Kai-Plus-98_1)
        // 5. "結尾" (DFKai-SB)
        Assert.Equal(5, segments.Count);

        Assert.Equal("測試", segments[0].Text);
        Assert.Equal(defaultFont, segments[0].FontName);

        Assert.Equal(plane2Char, segments[1].Text);
        Assert.Equal("TW-Kai-Ext-B-98_1", segments[1].FontName);

        Assert.Equal("中文字", segments[2].Text);
        Assert.Equal(defaultFont, segments[2].FontName);

        Assert.Equal(plane15Char, segments[3].Text);
        Assert.Equal("TW-Kai-Plus-98_1", segments[3].FontName);

        Assert.Equal("結尾", segments[4].Text);
        Assert.Equal(defaultFont, segments[4].FontName);
    }

    /// <summary>
    /// 驗證註冊字型子集化擴充點後，存檔會掃描 PUA 字元、嵌入子集字型並更新 font-face-uri。
    /// </summary>
    [Fact]
    public void FontSubsetterRegistration_EmbedsPrivateUseFontSubsetOnSave()
    {
        var subsetter = new FakeFontSubsetter();
        using IDisposable registration = OdfFontResolver.RegisterFontSubsetter(subsetter);
        using TextDocument document = TextDocument.Create();
        string pua = char.ConvertFromUtf32(0xF0000);

        document.AddFontFace("PuaFont", "PuaFont", "system-serif", "variable");
        document.AddParagraph("自造字" + pua);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);

        Assert.Single(subsetter.Requests);
        OdfFontSubsetRequest request = subsetter.Requests.Single();
        Assert.Equal("PuaFont", request.FontName);
        Assert.Contains(0xF0000, request.CodePoints);
        Assert.True(document.Package.HasEntry("Fonts/Subsets/PuaFont-subset.ttf"));
        Assert.Equal("font/ttf", document.Package.Manifest["Fonts/Subsets/PuaFont-subset.ttf"]);

        string contentXml = ReadEntry(document.Package, "content.xml");
        Assert.Contains("xlink:href=\"Fonts/Subsets/PuaFont-subset.ttf\"", contentXml, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派全字庫宋體字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_SongBaseline_MapsToSongFonts()
    {
        string baseFont = "TW-Song"; // 應判定為宋體/明體家族

        Assert.Equal("TW-Song-Ext-B-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("TW-Song-Plus-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("TW-Song-Plus-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("TW-Song-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派全字庫楷體字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_KaiBaseline_MapsToKaiFonts()
    {
        string baseFont = "DFKai-SB"; // 標楷體，應判定為楷體家族

        Assert.Equal("TW-Kai-Ext-B-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("TW-Kai-Plus-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("TW-Kai-Plus-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("TW-Kai-98_1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派花園明朝字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_HanaMinBaseline_MapsToHanaMinFonts()
    {
        string baseFont = "HanaMinA"; // 花園明朝，應判定為 HanaMin 家族

        Assert.Equal("HanaMinB", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("HanaMinB", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("HanaMinB", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("HanaMinA", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派字雲（Jigmo）字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_JigmoBaseline_MapsToJigmoFonts()
    {
        string baseFont = "Jigmo"; // 字雲字型

        Assert.Equal("Jigmo2", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("Jigmo3", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 3));
        Assert.Equal("Jigmo", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證混有 Unicode Plane 3（Ext.G/H）字元時，正確分割為多個文字片段並指派對應的 Jigmo 字型。
    /// </summary>
    [Fact]
    public void SegmentText_WithPlane3Characters_SegmentsCorrectly()
    {
        // 𰀀 為 Plane 3 字元 (U+30000, Ext.G 第一字)
        string plane3Char = char.ConvertFromUtf32(0x30000);
        string text = "前段" + plane3Char + "後段";
        string defaultFont = "Jigmo";

        var segments = OdfFontSegmenter.SegmentText(text, defaultFont);

        Assert.Equal(3, segments.Count);
        Assert.Equal("前段", segments[0].Text);
        Assert.Equal("Jigmo", segments[0].FontName);

        Assert.Equal(plane3Char, segments[1].Text);
        Assert.Equal("Jigmo3", segments[1].FontName);

        Assert.Equal("後段", segments[2].Text);
        Assert.Equal("Jigmo", segments[2].FontName);
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據 Windows 細明體、新細明體與中易宋體系統字型正確指派擴充字型（ExtB/ExtG）。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_WindowsSystemFonts_MapsToExtFonts()
    {
        Assert.Equal("MingLiU-ExtB", OdfFontSegmenter.GetSupplementaryPlaneFontName("MingLiU", 2));
        Assert.Equal("PMingLiU-ExtB", OdfFontSegmenter.GetSupplementaryPlaneFontName("PMingLiU", 2));
        Assert.Equal("MingLiU_HKSCS-ExtB", OdfFontSegmenter.GetSupplementaryPlaneFontName("MingLiU_HKSCS", 2));
        Assert.Equal("SimSun-ExtG", OdfFontSegmenter.GetSupplementaryPlaneFontName("MingLiU", 3));

        Assert.Equal("SimSun-ExtB", OdfFontSegmenter.GetSupplementaryPlaneFontName("SimSun", 2));
        Assert.Equal("SimSun-ExtG", OdfFontSegmenter.GetSupplementaryPlaneFontName("SimSun", 3));
        Assert.Equal("NSimSun", OdfFontSegmenter.GetSupplementaryPlaneFontName("NSimSun", 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否將不需要進行超大型拆分對照的常規字型（如思源黑體、Noto Sans、微軟正黑體等）直接回傳原字型名稱。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_RegularFonts_ReturnsOriginalName()
    {
        string baseFont1 = "Source Han Sans TC";
        string baseFont2 = "Noto Sans CJK TC";
        string baseFont3 = "Microsoft JhengHei";
        string baseFont4 = "Arial";

        Assert.Equal(baseFont1, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont1, 2));
        Assert.Equal(baseFont1, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont1, 3));
        Assert.Equal(baseFont1, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont1, 15));

        Assert.Equal(baseFont2, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont2, 2));
        Assert.Equal(baseFont2, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont2, 15));

        Assert.Equal(baseFont3, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont3, 2));
        Assert.Equal(baseFont3, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont3, 15));

        Assert.Equal(baseFont4, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont4, 2));
        Assert.Equal(baseFont4, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont4, 15));
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FakeFontSubsetter : IFontSubsetter
    {
        public List<OdfFontSubsetRequest> Requests { get; } = [];

        public OdfFontSubset? CreateSubset(OdfFontSubsetRequest request)
        {
            Requests.Add(request);
            return new OdfFontSubset([0x00, 0x01, 0x02], ".ttf", "font/ttf");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfFontContext 的文字字面分段與字型指派之單元測試。
/// </summary>
public class OdfFontSegmenterTests
{
    /// <summary>
    /// 驗證當輸入為空字串或 null 時，分段結果回傳空集合。
    /// </summary>
    [Fact]
    public void SegmentText_WithEmptyOrNull_ReturnsEmptyList()
    {
        var result1 = OdfFontContext.Default.SegmentText(null!, "TW-Kai");
        var result2 = OdfFontContext.Default.SegmentText(string.Empty, "TW-Kai");

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

        var segments = OdfFontContext.Default.SegmentText(text, defaultFont);

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

        var segments = OdfFontContext.Default.SegmentText(text, defaultFont);

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
        using IDisposable registration = OdfFontContext.Default.RegisterFontSubsetter(subsetter);
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
    /// 驗證預設 CJK 字型遞補宣告包含全字庫楷體與宋體家族。
    /// </summary>
    [Fact]
    public void ApplyCjkFontFallback_DeclaresTaiwanFullFontSetFamilies()
    {
        using TextDocument document = TextDocument.Create();

        document.ApplyCjkFontFallback();

        AssertFontFace(document.ContentDom, "TW-Kai-98_1");
        AssertFontFace(document.ContentDom, "TW-Kai-Ext-B-98_1");
        AssertFontFace(document.ContentDom, "TW-Kai-Plus-98_1");
        AssertFontFace(document.ContentDom, "TW-Song-98_1");
        AssertFontFace(document.ContentDom, "TW-Song-Ext-B-98_1");
        AssertFontFace(document.ContentDom, "TW-Song-Plus-98_1");
        AssertFontFace(document.ContentDom, "PMingLiU");
        AssertFontFace(document.ContentDom, "Microsoft JhengHei");
        AssertFontFace(document.StylesDom, "TW-Kai-Ext-B-98_1");
        AssertFontFace(document.StylesDom, "TW-Song-Ext-B-98_1");
    }

    /// <summary>
    /// 驗證 CNS 11643 字型遞補設定會正規化無效的基礎字型名稱。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cns11643Options_WithMissingBaseFont_UsesDefaultKaiFont(string? baseFont)
    {
        OdfTextFontFallbackOptions options = OdfTextFontFallbackOptions.Cns11643(baseFont);

        Assert.Equal("TW-Kai", options.BaseFont);
        Assert.True(options.DeclareDefaultCjkFallbackFonts);
    }

    /// <summary>
    /// 驗證直接建立字型遞補設定時仍會保證基礎字型名稱有效。
    /// </summary>
    [Fact]
    public void TextFontFallbackOptions_WithMissingBaseFont_UsesDefaultKaiFont()
    {
        var options = new OdfTextFontFallbackOptions(null, declareDefaultCjkFallbackFonts: false);

        Assert.Equal("TW-Kai", options.BaseFont);
        Assert.False(options.DeclareDefaultCjkFallbackFonts);
    }

    /// <summary>
    /// 驗證段落高階 API 可自動依 CNS 11643 全字庫情境分段並套用字型。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithFallbackOptions_SegmentsTextRunsAndDeclaresFallbackFonts()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        string plane15Char = char.ConvertFromUtf32(0xF0000);

        IReadOnlyList<OdfTextRun> runs = paragraph.AddText(
            "甲" + plane2Char + "乙" + plane15Char,
            OdfTextFontFallbackOptions.Cns11643("TW-Kai"));

        Assert.Equal(4, runs.Count);
        Assert.Equal("甲", runs[0].Text);
        Assert.Equal("TW-Kai", runs[0].FontName);
        Assert.Equal(plane2Char, runs[1].Text);
        Assert.Equal("TW-Kai-Ext-B-98_1", runs[1].FontName);
        Assert.Equal("乙", runs[2].Text);
        Assert.Equal("TW-Kai", runs[2].FontName);
        Assert.Equal(plane15Char, runs[3].Text);
        Assert.Equal("TW-Kai-Plus-98_1", runs[3].FontName);
        Assert.All(runs, run => Assert.Equal(run.FontName, run.FontNameAsian));
        AssertFontFace(document.ContentDom, "TW-Kai-Ext-B-98_1");
        AssertFontFace(document.ContentDom, "TW-Kai-Plus-98_1");
    }

    /// <summary>
    /// 驗證花園明朝 profile 會分段並宣告 HanaMin font-face。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithHanaMinOptions_SegmentsAndDeclaresFontFaces()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        string plane15Char = char.ConvertFromUtf32(0xF0000);

        IReadOnlyList<OdfTextRun> runs = paragraph.AddText(
            "甲" + plane2Char + plane15Char,
            OdfTextFontFallbackOptions.HanaMin());

        Assert.Equal(2, runs.Count);
        Assert.Equal("HanaMinA", runs[0].FontName);
        Assert.Equal("HanaMinB", runs[1].FontName);
        AssertFontFace(document.ContentDom, "HanaMinA");
        AssertFontFace(document.ContentDom, "HanaMinB");
        AssertFontFace(document.StylesDom, "HanaMinA");
        AssertFontFace(document.StylesDom, "HanaMinB");
    }

    /// <summary>
    /// 驗證字雲 profile 會分段並宣告 Jigmo font-face。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithJigmoOptions_SegmentsAndDeclaresFontFaces()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        string plane3Char = char.ConvertFromUtf32(0x30000);

        IReadOnlyList<OdfTextRun> runs = paragraph.AddText(
            "甲" + plane2Char + plane3Char,
            OdfTextFontFallbackOptions.Jigmo());

        Assert.Equal(3, runs.Count);
        Assert.Equal("Jigmo", runs[0].FontName);
        Assert.Equal("Jigmo2", runs[1].FontName);
        Assert.Equal("Jigmo3", runs[2].FontName);
        AssertFontFace(document.ContentDom, "Jigmo");
        AssertFontFace(document.ContentDom, "Jigmo2");
        AssertFontFace(document.ContentDom, "Jigmo3");
        AssertFontFace(document.StylesDom, "Jigmo");
        AssertFontFace(document.StylesDom, "Jigmo2");
        AssertFontFace(document.StylesDom, "Jigmo3");
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派全字庫宋體字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_SongBaseline_MapsToSongFonts()
    {
        string baseFont = "TW-Song"; // 應判定為宋體/明體家族

        Assert.Equal("TW-Song-Ext-B-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("TW-Song-Plus-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("TW-Song-Plus-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("TW-Song-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派全字庫楷體字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_KaiBaseline_MapsToKaiFonts()
    {
        string baseFont = "DFKai-SB"; // 標楷體，應判定為楷體家族

        Assert.Equal("TW-Kai-Ext-B-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("TW-Kai-Plus-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("TW-Kai-Plus-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("TW-Kai-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派花園明朝字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_HanaMinBaseline_MapsToHanaMinFonts()
    {
        string baseFont = "HanaMinA"; // 花園明朝，應判定為 HanaMin 家族

        Assert.Equal("HanaMinB", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("HanaMinB", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 15));
        Assert.Equal("HanaMinB", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 16));
        Assert.Equal("HanaMinA", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 0));
    }

    /// <summary>
    /// 驗證 GetSupplementaryPlaneFontName 是否依據基礎字型名稱與平面正確指派字雲（Jigmo）字型。
    /// </summary>
    [Fact]
    public void GetSupplementaryPlaneFontName_JigmoBaseline_MapsToJigmoFonts()
    {
        string baseFont = "Jigmo"; // 字雲字型

        Assert.Equal("Jigmo2", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal("Jigmo3", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 3));
        Assert.Equal("Jigmo", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 0));
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

        var segments = OdfFontContext.Default.SegmentText(text, defaultFont);

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
        Assert.Equal("MingLiU-ExtB", OdfFontContext.Default.GetSupplementaryPlaneFontName("MingLiU", 2));
        Assert.Equal("PMingLiU-ExtB", OdfFontContext.Default.GetSupplementaryPlaneFontName("PMingLiU", 2));
        Assert.Equal("MingLiU_HKSCS-ExtB", OdfFontContext.Default.GetSupplementaryPlaneFontName("MingLiU_HKSCS", 2));
        Assert.Equal("SimSun-ExtG", OdfFontContext.Default.GetSupplementaryPlaneFontName("MingLiU", 3));

        Assert.Equal("SimSun-ExtB", OdfFontContext.Default.GetSupplementaryPlaneFontName("SimSun", 2));
        Assert.Equal("SimSun-ExtG", OdfFontContext.Default.GetSupplementaryPlaneFontName("SimSun", 3));
        Assert.Equal("NSimSun", OdfFontContext.Default.GetSupplementaryPlaneFontName("NSimSun", 0));
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

        Assert.Equal(baseFont1, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont1, 2));
        Assert.Equal(baseFont1, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont1, 3));
        Assert.Equal(baseFont1, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont1, 15));

        Assert.Equal(baseFont2, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont2, 2));
        Assert.Equal(baseFont2, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont2, 15));

        Assert.Equal(baseFont3, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont3, 2));
        Assert.Equal(baseFont3, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont3, 15));

        Assert.Equal(baseFont4, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont4, 2));
        Assert.Equal(baseFont4, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont4, 15));
    }

    /// <summary>
    /// 驗證自訂平面對應註冊後可導向自訂字型，且 Dispose 後還原為原字型名稱。
    /// </summary>
    [Fact]
    public void RegisterSupplementaryPlaneFontMapping_RoutesToCustomFontsAndRestoresOnDispose()
    {
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        string plane3Char = char.ConvertFromUtf32(0x30000);
        const string baseFont = "FakeGothic-UnitTest";

        using (OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
            baseFont,
            new Dictionary<int, string> { [2] = "FakeGothic P1", [3] = "FakeGothic P2" }))
        {
            var segments = OdfFontContext.Default.SegmentText("前" + plane2Char + plane3Char + "後", baseFont);

            Assert.Equal(4, segments.Count);
            Assert.Equal(baseFont, segments[0].FontName);
            Assert.Equal("FakeGothic P1", segments[1].FontName);
            Assert.Equal("FakeGothic P2", segments[2].FontName);
            Assert.Equal(baseFont, segments[3].FontName);
        }

        // Dispose 後規則移除：增補平面字元不再改派字型
        var restored = OdfFontContext.Default.SegmentText(plane2Char, baseFont);
        Assert.Single(restored);
        Assert.Equal(baseFont, restored[0].FontName);
    }

    /// <summary>
    /// 驗證自訂平面對應優先於內建規則，且命中規則後未列出的平面維持基礎字型（不再回退內建規則）。
    /// </summary>
    [Fact]
    public void RegisterSupplementaryPlaneFontMapping_TakesPrecedenceOverBuiltInRules()
    {
        // BiauKai-UnitTest 含 "BiauKai"，未註冊時會命中內建楷體規則
        const string baseFont = "BiauKai-UnitTest";
        Assert.Equal("TW-Kai-Ext-B-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));

        using (OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
            baseFont,
            new Dictionary<int, string> { [2] = "Custom-ExtB" }))
        {
            // Plane 2 由自訂規則決定
            Assert.Equal("Custom-ExtB", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
            // Plane 15 未列於自訂規則：維持基礎字型，不回退內建的 TW-Kai-Plus-98_1
            Assert.Equal(baseFont, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 15));
        }

        // 還原後內建規則恢復生效
        Assert.Equal("TW-Kai-Ext-B-98_1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
    }

    /// <summary>
    /// 驗證自訂平面對應可涵蓋內建規則不處理的 Plane 1（SMP），且後註冊規則優先。
    /// </summary>
    [Fact]
    public void RegisterSupplementaryPlaneFontMapping_SupportsPlane1AndLaterRegistrationWins()
    {
        string plane1Char = char.ConvertFromUtf32(0x1F600);
        const string baseFont = "FakeSymbol-UnitTest";

        using IDisposable first = OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
            baseFont, new Dictionary<int, string> { [1] = "Symbols-Old" });
        using IDisposable second = OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
            baseFont, new Dictionary<int, string> { [1] = "Symbols-New" });

        var segments = OdfFontContext.Default.SegmentText("文" + plane1Char, baseFont);

        Assert.Equal(2, segments.Count);
        Assert.Equal(baseFont, segments[0].FontName);
        Assert.Equal("Symbols-New", segments[1].FontName);
    }

    /// <summary>
    /// 驗證自訂平面對應的參數驗證：空白模式、null 字典、平面編號超界與空白字型名稱。
    /// </summary>
    [Fact]
    public void RegisterSupplementaryPlaneFontMapping_ValidatesArguments()
    {
        var valid = new Dictionary<int, string> { [2] = "Font" };

        Assert.Throws<ArgumentNullException>(
            () => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping("", valid));
        Assert.Throws<ArgumentNullException>(
            () => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping("Pattern", null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
                "Pattern", new Dictionary<int, string> { [0] = "Font" }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
                "Pattern", new Dictionary<int, string> { [17] = "Font" }));
        Assert.Throws<ArgumentException>(
            () => OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
                "Pattern", new Dictionary<int, string> { [2] = " " }));
    }

    /// <summary>
    /// 驗證註冊後修改原字典不影響既有規則（防禦性複製）。
    /// </summary>
    [Fact]
    public void RegisterSupplementaryPlaneFontMapping_CopiesMappingDefensively()
    {
        const string baseFont = "FakeCopy-UnitTest";
        var planeFonts = new Dictionary<int, string> { [2] = "Copied-Font" };

        using IDisposable registration = OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(baseFont, planeFonts);
        planeFonts[2] = "Mutated-Font";
        planeFonts[3] = "Injected-Font";

        Assert.Equal("Copied-Font", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal(baseFont, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 3));
    }

    /// <summary>
    /// 驗證 Custom 遞補選項會宣告自訂 font-face，並可搭配自訂平面對應讓段落分段導向自訂字型。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithCustomOptions_SegmentsAndDeclaresCustomFontFaces()
    {
        using IDisposable registration = OdfFontContext.Default.RegisterSupplementaryPlaneFontMapping(
            "FakeCustom-UnitTest",
            new Dictionary<int, string> { [2] = "FakeCustom P1", [3] = "FakeCustom P2" });
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        string plane3Char = char.ConvertFromUtf32(0x30000);

        IReadOnlyList<OdfTextRun> runs = paragraph.AddText(
            "甲" + plane2Char + plane3Char,
            OdfTextFontFallbackOptions.Custom(
                "FakeCustom-UnitTest",
                [
                    new OdfFontFaceInfo("FakeCustom P1", "FakeCustom P1", "system-sans-serif", "variable"),
                    new OdfFontFaceInfo("FakeCustom P2", "FakeCustom P2", "system-sans-serif", "variable")
                ]));

        Assert.Equal(3, runs.Count);
        Assert.Equal("FakeCustom-UnitTest", runs[0].FontName);
        Assert.Equal("FakeCustom P1", runs[1].FontName);
        Assert.Equal("FakeCustom P2", runs[2].FontName);
        AssertFontFace(document.ContentDom, "FakeCustom P1");
        AssertFontFace(document.ContentDom, "FakeCustom P2");
        AssertFontFace(document.StylesDom, "FakeCustom P1");
        AssertFontFace(document.StylesDom, "FakeCustom P2");
    }

    /// <summary>
    /// 驗證 Custom 遞補選項的參數驗證與防禦性複製行為。
    /// </summary>
    [Fact]
    public void CustomOptions_ValidatesAndCopiesFontFaces()
    {
        Assert.Throws<ArgumentNullException>(
            () => OdfTextFontFallbackOptions.Custom("Base", null!));
        Assert.Throws<ArgumentException>(
            () => OdfTextFontFallbackOptions.Custom("Base", [null!]));
        Assert.Throws<ArgumentException>(
            () => OdfTextFontFallbackOptions.Custom(
                "Base", [new OdfFontFaceInfo("", "Family", null, null)]));
        Assert.Throws<ArgumentException>(
            () => OdfTextFontFallbackOptions.Custom(
                "Base", [new OdfFontFaceInfo("Name", " ", null, null)]));

        OdfTextFontFallbackOptions options = OdfTextFontFallbackOptions.Custom(
            "Base", [new OdfFontFaceInfo("Name", "Family", null, null)]);
        Assert.Equal("Base", options.BaseFont);
        Assert.True(options.DeclareDefaultCjkFallbackFonts);
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void AssertFontFace(OdfNode domRoot, string name)
    {
        OdfNode? fontFace = domRoot.Descendants().FirstOrDefault(
            node => node.LocalName == "font-face" &&
                    node.NamespaceUri == OdfNamespaces.Style &&
                    node.GetAttribute("name", OdfNamespaces.Style) == name);

        Assert.NotNull(fontFace);
        Assert.Equal(name, fontFace.GetAttribute("font-family", OdfNamespaces.Svg));
        Assert.Equal("variable", fontFace.GetAttribute("font-pitch", OdfNamespaces.Style));
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

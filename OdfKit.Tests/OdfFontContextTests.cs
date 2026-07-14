using System;
using System.Collections.Generic;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfFontContext 實例隔離與靜態 API 轉發行為之單元測試。
/// </summary>
public class OdfFontContextTests
{
    /// <summary>
    /// 驗證不同 OdfFontContext 執行個體的平面對應彼此隔離，且不影響 Default 情境。
    /// </summary>
    [Fact]
    public void PlaneFontMappings_AreIsolatedBetweenContexts()
    {
        var contextA = new OdfFontContext();
        var contextB = new OdfFontContext();
        const string baseFont = "IsolatedGothic-UnitTest";
        string plane2Char = char.ConvertFromUtf32(0x20BB7);

        using IDisposable registration = contextA.RegisterSupplementaryPlaneFontMapping(
            baseFont, new Dictionary<int, string> { [2] = "Isolated P1" });

        // contextA 命中自訂規則
        Assert.Equal("Isolated P1", contextA.GetSupplementaryPlaneFontName(baseFont, 2));
        // contextB 與 Default 不受影響：走內建規則（無命中，回傳原字型）
        Assert.Equal(baseFont, contextB.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal(baseFont, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));

        var segments = contextA.SegmentText("前" + plane2Char, baseFont);
        Assert.Equal(2, segments.Count);
        Assert.Equal("Isolated P1", segments[1].FontName);
    }

    /// <summary>
    /// 驗證字型替代規則（RegisterFallback／MapFont）在情境之間彼此隔離。
    /// </summary>
    [Fact]
    public void FallbackRegistrations_AreIsolatedBetweenContexts()
    {
        var contextA = new OdfFontContext();
        var contextB = new OdfFontContext();
        const string target = "IsolatedTarget-UnitTest";

        contextA.RegisterFallback(target, "Replacement-A");

        Assert.Equal("Replacement-A", contextA.MapFont(target));
        Assert.Equal(target, contextB.MapFont(target));
        Assert.Equal(target, OdfFontContext.Default.MapFont(target));
    }

    /// <summary>
    /// 驗證 OdfFontSegmenter 靜態 API 轉發至 Default 情境（雙向可觀察）。
    /// </summary>
    [Fact]
    public void StaticSegmenterApi_ForwardsToDefaultContext()
    {
        const string baseFont = "ForwardGothic-UnitTest";

        using (OdfFontSegmenter.RegisterSupplementaryPlaneFontMapping(
            baseFont, new Dictionary<int, string> { [2] = "Forwarded P1" }))
        {
            // 靜態註冊經由 Default 情境可見
            Assert.Equal("Forwarded P1", OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
            Assert.Equal("Forwarded P1", OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
        }

        // Dispose 後兩側同步還原
        Assert.Equal(baseFont, OdfFontContext.Default.GetSupplementaryPlaneFontName(baseFont, 2));
        Assert.Equal(baseFont, OdfFontSegmenter.GetSupplementaryPlaneFontName(baseFont, 2));
    }

    /// <summary>
    /// 驗證 OdfTextFontFallbackOptions.FontContext 可將段落分段路由至指定情境，且不影響 Default。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithFontContextOption_RoutesSegmentationToContext()
    {
        var context = new OdfFontContext();
        const string baseFont = "RoutedGothic-UnitTest";
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        using IDisposable registration = context.RegisterSupplementaryPlaneFontMapping(
            baseFont, new Dictionary<int, string> { [2] = "Routed P1" });

        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        OdfTextFontFallbackOptions options = new(baseFont, declareDefaultCjkFallbackFonts: false)
        {
            FontContext = context
        };

        IReadOnlyList<OdfTextRun> runs = paragraph.AddText("甲" + plane2Char, options);

        Assert.Equal(2, runs.Count);
        Assert.Equal(baseFont, runs[0].FontName);
        Assert.Equal("Routed P1", runs[1].FontName);

        // Default 情境不受影響
        var defaultSegments = OdfFontContext.Default.SegmentText(plane2Char, baseFont);
        Assert.Single(defaultSegments);
        Assert.Equal(baseFont, defaultSegments[0].FontName);
    }

    /// <summary>
    /// 驗證 FontContext 為 null 時分段走 Default 情境（既有行為不變）。
    /// </summary>
    [Fact]
    public void ParagraphAddText_WithoutFontContext_UsesDefaultContext()
    {
        var options = new OdfTextFontFallbackOptions("TW-Kai", declareDefaultCjkFallbackFonts: false);

        Assert.Null(options.FontContext);
        Assert.Same(OdfFontContext.Default, options.EffectiveFontContext);
    }
}

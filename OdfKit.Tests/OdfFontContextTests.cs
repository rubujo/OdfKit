using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
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
    public void PlaneFontMappingsAreIsolatedBetweenContexts()
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
    public void FallbackRegistrationsAreIsolatedBetweenContexts()
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
    /// 驗證 Default 為穩定單例，且文件與選項未指定情境時皆解析至 Default。
    /// </summary>
    [Fact]
    public void DefaultContextIsStableSingleton()
    {
        Assert.Same(OdfFontContext.Default, OdfFontContext.Default);

        using TextDocument document = TextDocument.Create();
        Assert.Same(OdfFontContext.Default, document.FontContext);
    }

    /// <summary>
    /// 驗證 OdfDocument.FontContext 拒絕 null 並可指派自訂情境。
    /// </summary>
    [Fact]
    public void DocumentFontContextValidatesAndAcceptsCustomContext()
    {
        using TextDocument document = TextDocument.Create();
        var context = new OdfFontContext();

        Assert.Throws<ArgumentNullException>(() => document.FontContext = null!);

        document.FontContext = context;
        Assert.Same(context, document.FontContext);
    }

    /// <summary>
    /// 驗證存檔時的字型子集化內嵌使用文件層級的字型情境，且 Default 情境不受影響。
    /// </summary>
    [Fact]
    public void SaveWithDocumentFontContextEmbedsSubsetsViaDocumentContext()
    {
        var context = new OdfFontContext();
        var subsetter = new RecordingFontSubsetter();
        using IDisposable registration = context.RegisterFontSubsetter(subsetter);

        using TextDocument document = TextDocument.Create();
        document.FontContext = context;
        document.AddFontFace("CtxFont", "CtxFont", "system-serif", "variable");
        document.AddParagraph("自造字" + char.ConvertFromUtf32(0xF0000));

        using var stream = new System.IO.MemoryStream();
        document.SaveToStream(stream);

        // 子集化器只註冊於文件的情境，仍被存檔管線呼叫 → 內嵌走的是文件情境
        Assert.Contains(subsetter.Requests, request => request.FontName == "CtxFont");
        Assert.True(document.Package.HasEntry("Fonts/Subsets/CtxFont-subset.ttf"));
    }

    /// <summary>
    /// 驗證 OdfTextFontFallbackOptions.FontContext 可將段落分段路由至指定情境，且不影響 Default。
    /// </summary>
    [Fact]
    public void ParagraphAddTextWithFontContextOptionRoutesSegmentationToContext()
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
    /// 驗證 FontContext 解析優先序：選項優先、其次文件、最後 Default。
    /// </summary>
    [Fact]
    public void ResolveFontContextHonorsOptionThenDocumentThenDefault()
    {
        var documentContext = new OdfFontContext();
        var optionContext = new OdfFontContext();
        using TextDocument document = TextDocument.Create();
        document.FontContext = documentContext;

        var bareOptions = new OdfTextFontFallbackOptions("TW-Kai", declareDefaultCjkFallbackFonts: false);
        var optionScoped = new OdfTextFontFallbackOptions("TW-Kai", declareDefaultCjkFallbackFonts: false)
        {
            FontContext = optionContext
        };

        Assert.Same(optionContext, optionScoped.ResolveFontContext(document));
        Assert.Same(documentContext, bareOptions.ResolveFontContext(document));
        Assert.Same(OdfFontContext.Default, bareOptions.ResolveFontContext(null));
    }

    /// <summary>
    /// 驗證圖表主標題與座標軸標題的字型遞補多載會分段、套用字型並宣告 font-face。
    /// </summary>
    [Fact]
    public void ChartTitlesWithFallbackOptionsSegmentAndDeclareFontFaces()
    {
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        using Chart.ChartDocument chartDoc = Chart.ChartDocument.Builder()
            .WithTitle("甲" + plane2Char, OdfTextFontFallbackOptions.Cns11643("TW-Kai"))
            .WithAxis("x", axis => axis.WithTitle("乙" + plane2Char, OdfTextFontFallbackOptions.Cns11643("TW-Kai")))
            .Build();

        // 主標題與軸標題的文字內容完整保留
        Assert.Equal("甲" + plane2Char, chartDoc.ChartTitle);
        Assert.Equal("乙" + plane2Char, chartDoc.FindAxisTitle("x"));

        // 分段結果以 text:span 寫入且宣告了全字庫 font-face
        Assert.Contains(
            chartDoc.ContentDom.Descendants(),
            node => node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(
            chartDoc.ContentDom.Descendants(),
            node => node.LocalName == "font-face" &&
                    node.GetAttribute("name", OdfNamespaces.Style) == "TW-Kai-Ext-B-98_1");
    }

    /// <summary>
    /// 驗證圖表標題多載的空白清除語意與參數驗證。
    /// </summary>
    [Fact]
    public void SetChartTitleWithFallbackOptionsValidatesAndClearsBlankTitle()
    {
        using Chart.ChartDocument chartDoc = Chart.ChartDocument.Builder().Build();
        OdfTextFontFallbackOptions options = OdfTextFontFallbackOptions.Cns11643("TW-Kai");

        Assert.Throws<ArgumentNullException>(() => chartDoc.SetChartTitle("標題", null!));
        Assert.Throws<ArgumentNullException>(() => chartDoc.SetAxisTitle("x", "標題", null!));

        chartDoc.SetChartTitle("標題", options);
        Assert.Equal("標題", chartDoc.ChartTitle);

        chartDoc.SetChartTitle(" ", options);
        Assert.True(string.IsNullOrEmpty(chartDoc.ChartTitle));
    }

    /// <summary>
    /// 驗證簡報嵌入表格儲存格的字型遞補多載會分段、套用字型並宣告 font-face。
    /// </summary>
    [Fact]
    public void EmbeddedTableSetCellTextWithFallbackOptionsSegmentsAndDeclaresFontFaces()
    {
        string plane2Char = char.ConvertFromUtf32(0x20BB7);
        using Presentation.PresentationDocument document = Presentation.PresentationDocument.Create();
        Presentation.OdfSlide slide = document.Slides.Add("Tables");
        Presentation.OdfEmbeddedTable table = slide.AddTable(
            2, 2,
            OdfLength.Parse("1cm"), OdfLength.Parse("1cm"),
            OdfLength.Parse("12cm"), OdfLength.Parse("6cm"));

        Assert.Throws<ArgumentNullException>(
            () => table.SetCellText(0, 0, null!, OdfTextFontFallbackOptions.Cns11643("TW-Kai")));
        Assert.Throws<ArgumentNullException>(
            () => table.SetCellText(0, 0, "文", null!));

        table.SetCellText(0, 0, "甲" + plane2Char, OdfTextFontFallbackOptions.Cns11643("TW-Kai"));

        Assert.Equal("甲" + plane2Char, table.GetCellText(0, 0));
        Assert.Contains(
            table.TableNode.Descendants(),
            node => node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text);
        Assert.Contains(
            document.ContentDom.Descendants(),
            node => node.LocalName == "font-face" &&
                    node.GetAttribute("name", OdfNamespaces.Style) == "TW-Kai-Ext-B-98_1");
    }

    /// <summary>
    /// 驗證 Custom 三參數多載可同時攜帶自訂 font-face 與字型情境，短多載維持 null 情境。
    /// </summary>
    [Fact]
    public void CustomOptionsWithFontContextCarriesContextThroughFactory()
    {
        var context = new OdfFontContext();
        using IDisposable registration = context.RegisterSupplementaryPlaneFontMapping(
            "Base", new Dictionary<int, string> { [2] = "Name" });
        using TextDocument document = TextDocument.Create();

        OdfTextFontFallbackOptions options = OdfTextFontFallbackOptions.Custom(
            "Base",
            [
                new OdfFontFaceInfo("Base", "Base Family", null, null),
                new OdfFontFaceInfo("Name", "Family", null, null)
            ],
            context);
        Assert.Same(context, options.FontContext);

        IReadOnlyList<OdfTextRun> runs = document.AddParagraph().AddText(
            "甲" + char.ConvertFromUtf32(0x20BB7), options);
        Assert.Equal(2, runs.Count);
        Assert.Equal("Base", runs[0].FontName);
        Assert.Equal("Name", runs[1].FontName);
        Assert.Contains(
            document.ContentDom.Descendants(),
            node => node.LocalName == "font-face" &&
                node.NamespaceUri == OdfNamespaces.Style &&
                node.GetAttribute("name", OdfNamespaces.Style) == "Name");
        Assert.Contains(
            document.ContentDom.Descendants(),
            node => node.LocalName == "font-face" &&
                node.NamespaceUri == OdfNamespaces.Style &&
                node.GetAttribute("name", OdfNamespaces.Style) == "Base");

        OdfTextFontFallbackOptions shortOverload = OdfTextFontFallbackOptions.Custom(
            "Base", [new OdfFontFaceInfo("Name", "Family", null, null)]);
        Assert.Null(shortOverload.FontContext);
    }

    private sealed class RecordingFontSubsetter : IFontSubsetter
    {
        public List<OdfFontSubsetRequest> Requests { get; } = [];

        public OdfFontSubset? CreateSubset(OdfFontSubsetRequest request)
        {
            Requests.Add(request);
            return new OdfFontSubset([0x00, 0x01], ".ttf", "font/ttf");
        }
    }
}

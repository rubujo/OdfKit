using System;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OTC 圖表範本與 FODC 扁平 XML 圖表的雙向轉換工作流。
/// </summary>
public class ChartVariantRoundTripTests
{
    /// <summary>
    /// 驗證 <see cref="ChartTemplateDocument.CreateFromDocument(ChartDocument)"/> 與
    /// <see cref="ChartDocument.CreateFromTemplate(ChartTemplateDocument)"/> 形成的雙向轉換，
    /// 圖表定義與序列設定完整保留。
    /// </summary>
    [Fact]
    public void ChartDocument_CreateTemplateFromDocument_RoundTripsBackToDocument()
    {
        var definition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "範本往返測試圖表",
            DataRange = new OdfCellRange(0, 0, 4, 1, "LocalTable"),
            HasLegend = true
        };

        using var original = ChartDocument.Create(definition);

        using var template = ChartTemplateDocument.CreateFromDocument(original);
        Assert.Equal("application/vnd.oasis.opendocument.chart-template", template.Package.MimeType);
        Assert.Equal(OdfDocumentKind.ChartTemplate, template.DocumentKind);
        Assert.Equal("範本往返測試圖表", template.ChartTitle);

        using var restored = ChartDocument.CreateFromTemplate(template);
        Assert.Equal("application/vnd.oasis.opendocument.chart", restored.Package.MimeType);
        Assert.Equal(OdfDocumentKind.Chart, restored.DocumentKind);
        Assert.Equal("範本往返測試圖表", restored.ChartTitle);
        Assert.Equal("chart:bar", restored.ChartClass);
    }

    /// <summary>
    /// 驗證 <see cref="FlatChartDocument.CreateFromDocument(ChartDocument)"/> 與
    /// <see cref="ChartDocument.CreateFromFlatDocument(FlatChartDocument)"/> 形成的雙向轉換，
    /// 圖表定義與序列設定完整保留，且 Flat 形態確實為單一 XML（非 ZIP）。
    /// </summary>
    [Fact]
    public void ChartDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        var definition = new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "Flat 往返測試圖表",
            DataRange = new OdfCellRange(0, 0, 4, 1, "LocalTable"),
            HasLegend = false
        };

        using var original = ChartDocument.Create(definition);

        using var flat = FlatChartDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.Equal(OdfDocumentKind.FlatChart, flat.DocumentKind);
        Assert.Equal("Flat 往返測試圖表", flat.ChartTitle);

        using var restored = ChartDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.Equal(OdfDocumentKind.Chart, restored.DocumentKind);
        Assert.Equal("Flat 往返測試圖表", restored.ChartTitle);
        Assert.Equal("chart:line", restored.ChartClass);
    }

    /// <summary>
    /// 驗證四個 Chart 雙向轉換工作流方法的邊界案例：
    /// 傳入 <see langword="null"/> 來源文件時皆擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void ChartVariantConversions_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ChartTemplateDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => ChartDocument.CreateFromTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => FlatChartDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => ChartDocument.CreateFromFlatDocument(null!));
    }
}

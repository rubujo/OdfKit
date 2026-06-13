using System;
using System.IO;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Chart、Formula、Image 與 Database 文件的最小高階入口。
/// </summary>
public class DocumentKindApiUsabilityTests
{
    /// <summary>
    /// 驗證文件層格式摘要會回報 Flat XML 種類與高階 wrapper。
    /// </summary>
    /// <param name="kind">要建立的 Flat XML ODF 種類。</param>
    /// <param name="contentKind">對應的內容種類。</param>
    /// <param name="expectedType">預期的高階 wrapper 類型。</param>
    /// <param name="extension">預期副檔名。</param>
    [Theory]
    [InlineData(OdfDocumentKind.FlatText, OdfDocumentKind.Text, typeof(TextDocument), ".fodt")]
    [InlineData(OdfDocumentKind.FlatSpreadsheet, OdfDocumentKind.Spreadsheet, typeof(SpreadsheetDocument), ".fods")]
    [InlineData(OdfDocumentKind.FlatPresentation, OdfDocumentKind.Presentation, typeof(PresentationDocument), ".fodp")]
    [InlineData(OdfDocumentKind.FlatGraphics, OdfDocumentKind.Graphics, typeof(DrawingDocument), ".fodg")]
    public void DocumentFormatSummaryReportsFlatXmlKinds(
        OdfDocumentKind kind,
        OdfDocumentKind contentKind,
        Type expectedType,
        string extension)
    {
        using OdfDocument document = OdfDocument.Create(kind);

        Assert.IsType(expectedType, document);
        Assert.Equal(kind, document.DocumentKind);
        Assert.Equal(contentKind, document.ContentKind);
        Assert.False(document.IsTemplate);
        Assert.True(document.IsFlatXml);
        OdfFormatInfo format = Assert.IsType<OdfFormatInfo>(document.Format);
        Assert.Equal(extension, format.Extension);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfDocument loaded = OdfDocument.Load(stream, "flat" + extension);
        Assert.IsType(expectedType, loaded);
        Assert.Equal(kind, loaded.DocumentKind);
        Assert.Equal(contentKind, loaded.ContentKind);
        Assert.True(loaded.IsFlatXml);
        Assert.Equal(extension, loaded.Format?.Extension);
    }

    /// <summary>
    /// 驗證文件層格式摘要會回報範本種類與內容種類。
    /// </summary>
    /// <param name="kind">要建立的 ODF 範本種類。</param>
    /// <param name="contentKind">對應的內容種類。</param>
    /// <param name="extension">預期副檔名。</param>
    [Theory]
    [InlineData(OdfDocumentKind.TextTemplate, OdfDocumentKind.Text, ".ott")]
    [InlineData(OdfDocumentKind.SpreadsheetTemplate, OdfDocumentKind.Spreadsheet, ".ots")]
    [InlineData(OdfDocumentKind.PresentationTemplate, OdfDocumentKind.Presentation, ".otp")]
    [InlineData(OdfDocumentKind.GraphicsTemplate, OdfDocumentKind.Graphics, ".otg")]
    public void DocumentFormatSummaryReportsTemplateKinds(
        OdfDocumentKind kind,
        OdfDocumentKind contentKind,
        string extension)
    {
        using OdfDocument document = OdfDocument.Create(kind);

        Assert.Equal(kind, document.DocumentKind);
        Assert.Equal(contentKind, document.ContentKind);
        Assert.True(document.IsTemplate);
        Assert.False(document.IsFlatXml);
        OdfFormatInfo format = Assert.IsType<OdfFormatInfo>(document.Format);
        Assert.Equal(extension, format.Extension);
        Assert.True(OdfDocumentKindDetector.IsTemplateKind(kind));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfDocument loaded = OdfDocument.Load(stream, "template" + extension);
        Assert.Equal(kind, loaded.DocumentKind);
        Assert.Equal(contentKind, loaded.ContentKind);
        Assert.True(loaded.IsTemplate);
        Assert.Equal(extension, loaded.Format?.Extension);
    }

    /// <summary>
    /// 驗證文件層格式摘要會回報主控文字文件種類。
    /// </summary>
    [Fact]
    public void DocumentFormatSummaryReportsTextMasterKind()
    {
        using OdfDocument document = OdfDocument.Create(OdfDocumentKind.TextMaster);

        Assert.Equal(OdfDocumentKind.TextMaster, document.DocumentKind);
        Assert.Equal(OdfDocumentKind.Text, document.ContentKind);
        Assert.False(document.IsTemplate);
        Assert.True(document.IsMasterDocument);
        Assert.False(document.IsFlatXml);
        Assert.Equal(".odm", document.Format?.Extension);
        Assert.True(OdfDocumentKindDetector.IsMasterKind(OdfDocumentKind.TextMaster));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfDocument loaded = OdfDocument.Load(stream, "master.odm");
        Assert.Equal(OdfDocumentKind.TextMaster, loaded.DocumentKind);
        Assert.Equal(OdfDocumentKind.Text, loaded.ContentKind);
        Assert.True(loaded.IsMasterDocument);
        Assert.Equal(".odm", loaded.Format?.Extension);
    }

    /// <summary>
    /// 驗證 ODC 圖表可建立標題、圖例、資料來源標籤、分類與序列佔位並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadChartWithTitleLegendAndSeries()
    {
        using var chart = OdfChartDocument.Create();
        chart.ChartClass = "bar";
        chart.ChartTitle = "營收";
        chart.DataSourceHasLabels = "both";
        chart.SetLegend("top");
        chart.SetCategories("Sheet1.A1:C1");
        chart.AddSeries("Sheet1.A1:A3", "Sheet1.A1");

        using OdfChartDocument loaded = RoundTrip(chart, "chart.odc", OdfChartDocument.Load);

        Assert.Equal("application/vnd.oasis.opendocument.chart", loaded.Package.MimeType);
        Assert.Equal("bar", loaded.ChartClass);
        Assert.Equal("營收", loaded.ChartTitle);
        Assert.Equal("both", loaded.DataSourceHasLabels);
        Assert.Equal("top", loaded.LegendPosition);
        Assert.Equal("Sheet1.A1:C1", loaded.CategoriesCellRangeAddress);
        Assert.Single(loaded.Series);
        Assert.Equal("Sheet1.A1:A3", loaded.Series[0].ValuesCellRangeAddress);
        Assert.Equal("Sheet1.A1", loaded.Series[0].LabelCellAddress);
        Assert.Equal(
            "both",
            FindDescendant(loaded.ChartNode, "plot-area", OdfNamespaces.Chart)?.GetAttribute("data-source-has-labels", OdfNamespaces.Chart));
        Assert.Equal(
            "Sheet1.A1:C1",
            FindDescendant(loaded.ChartNode, "categories", OdfNamespaces.Chart)?.GetAttribute("cell-range-address", OdfNamespaces.Table));
        Assert.Equal(
            "Sheet1.A1:A3",
            FindDescendant(loaded.ChartNode, "series", OdfNamespaces.Chart)?.GetAttribute("values-cell-range-address", OdfNamespaces.Chart));
    }

    /// <summary>
    /// 驗證 ODF 公式文件可設定 MathML 並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadFormulaWithMathMlRoot()
    {
        using var formula = OdfFormulaDocument.Create();
        formula.SetMathMl("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi><mo>=</mo><mn>1</mn></mrow></math>");

        using OdfFormulaDocument loaded = RoundTrip(formula, "formula.odf", OdfFormulaDocument.Load);

        Assert.Equal("application/vnd.oasis.opendocument.formula", loaded.Package.MimeType);
        Assert.Equal("math", loaded.MathNode.LocalName);
        Assert.Equal("x=1", loaded.MathText);
        Assert.Equal("x", FindDescendant(loaded.MathNode, "mi", "http://www.w3.org/1998/Math/MathML")?.TextContent);
    }

    /// <summary>
    /// 驗證 ODI 影像文件可嵌入圖片並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadImageWithEmbeddedPicture()
    {
        using var image = OdfImageDocument.Create();
        string href = image.SetImage(CreatePngBytes(), "TinyPng.png");

        using OdfImageDocument loaded = RoundTrip(image, "image.odi", OdfImageDocument.Load);

        Assert.Equal("application/vnd.oasis.opendocument.image", loaded.Package.MimeType);
        Assert.Equal(href, loaded.ImageHref);
        Assert.NotNull(loaded.ImageInfo);
        Assert.Equal(href, loaded.ImageInfo.Path);
        Assert.Equal("image/png", loaded.ImageInfo.MediaType);
        Assert.Equal(CreatePngBytes().Length, loaded.ImageInfo.Size);
        Assert.True(loaded.Package.HasEntry("Pictures/TinyPng.png"));
    }

    /// <summary>
    /// 驗證 ODB 資料庫文件可建立資料來源描述並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadDatabaseWithConnectionAndTable()
    {
        using var database = OdfDatabaseDocument.Create();
        database.SetConnection("sdbc:embedded:hsqldb");
        database.AddTable("Customers", "SELECT * FROM Customers");

        using OdfDatabaseDocument loaded = RoundTrip(database, "database.odb", OdfDatabaseDocument.Load);
        IReadOnlyList<OdfDatabaseTableInfo> tables = loaded.GetTables();

        Assert.Equal("application/vnd.oasis.opendocument.database", loaded.Package.MimeType);
        Assert.Equal("sdbc:embedded:hsqldb", loaded.ConnectionHref);
        Assert.Single(tables);
        Assert.Equal("Customers", tables[0].Name);
        Assert.Equal("SELECT * FROM Customers", tables[0].Command);
    }

    /// <summary>
    /// 驗證 typed 載入入口不會接受錯誤格式。
    /// </summary>
    [Fact]
    public void TypedLoadRejectsMismatchedDocumentKind()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidOperationException>(() => OdfChartDocument.Load(stream, "document.odt"));
    }

    private static TDocument RoundTrip<TDocument>(
        OdfDocument document,
        string fileName,
        Func<Stream, string?, TDocument> load)
        where TDocument : OdfDocument
    {
        var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;
        return load(stream, fileName);
    }

    private static OdfNode? FindDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindDescendant(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }
}

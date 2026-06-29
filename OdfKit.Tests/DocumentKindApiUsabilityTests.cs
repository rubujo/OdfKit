using System;
using System.Collections.Generic;
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
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Chart、Formula、Image 與 Database 文件的最小高階入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class DocumentKindApiUsabilityTests
{
    /// <summary>
    /// 驗證文件層格式摘要會回報 Flat XML 種類與高階 wrapper。
    /// </summary>
    /// <param name="kind">要建立的 Flat XML ODF 種類</param>
    /// <param name="contentKind">對應的內容種類</param>
    /// <param name="expectedType">預期的高階 wrapper 類型</param>
    /// <param name="extension">預期副檔名</param>
    [Theory]
    [InlineData(OdfDocumentKind.FlatText, OdfDocumentKind.Text, typeof(FlatTextDocument), ".fodt")]
    [InlineData(OdfDocumentKind.FlatSpreadsheet, OdfDocumentKind.Spreadsheet, typeof(FlatSpreadsheetDocument), ".fods")]
    [InlineData(OdfDocumentKind.FlatPresentation, OdfDocumentKind.Presentation, typeof(FlatPresentationDocument), ".fodp")]
    [InlineData(OdfDocumentKind.FlatGraphics, OdfDocumentKind.Graphics, typeof(FlatGraphicsDocument), ".fodg")]
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
    /// <param name="kind">要建立的 ODF 範本種類</param>
    /// <param name="contentKind">對應的內容種類</param>
    /// <param name="extension">預期副檔名</param>
    [Theory]
    [InlineData(OdfDocumentKind.TextTemplate, OdfDocumentKind.Text, typeof(TextTemplateDocument), ".ott")]
    [InlineData(OdfDocumentKind.SpreadsheetTemplate, OdfDocumentKind.Spreadsheet, typeof(SpreadsheetTemplateDocument), ".ots")]
    [InlineData(OdfDocumentKind.PresentationTemplate, OdfDocumentKind.Presentation, typeof(PresentationTemplateDocument), ".otp")]
    [InlineData(OdfDocumentKind.GraphicsTemplate, OdfDocumentKind.Graphics, typeof(GraphicsTemplateDocument), ".otg")]
    public void DocumentFormatSummaryReportsTemplateKinds(
        OdfDocumentKind kind,
        OdfDocumentKind contentKind,
        Type expectedType,
        string extension)
    {
        using OdfDocument document = OdfDocument.Create(kind);

        Assert.IsType(expectedType, document);
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
        Assert.IsType(expectedType, loaded);
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

        Assert.IsType<TextMasterDocument>(document);
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
        Assert.IsType<TextMasterDocument>(loaded);
        Assert.Equal(OdfDocumentKind.TextMaster, loaded.DocumentKind);
        Assert.Equal(OdfDocumentKind.Text, loaded.ContentKind);
        Assert.True(loaded.IsMasterDocument);
        Assert.Equal(".odm", loaded.Format?.Extension);
    }

    /// <summary>
    /// 驗證 ODC 圖表可建立標題、圖例、座標軸、資料來源標籤、分類與序列佔位並 round-trip。
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
        chart.XAxisTitle = "月份";
        chart.YAxisTitle = "金額";
        chart.AddSeries("Sheet1.A1:A3", "Sheet1.A1");

        using OdfChartDocument loaded = RoundTrip(chart, "chart.odc", OdfChartDocument.Load);
        OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, OdfComplianceProfiles.OasisOdf14Extended, "chart.odc");

        Assert.Equal("application/vnd.oasis.opendocument.chart", loaded.Package.MimeType);
        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal("bar", loaded.ChartClass);
        Assert.Equal("營收", loaded.ChartTitle);
        Assert.Equal("both", loaded.DataSourceHasLabels);
        Assert.Equal("top", loaded.LegendPosition);
        Assert.Equal("Sheet1.A1:C1", loaded.CategoriesCellRangeAddress);
        Assert.Equal("月份", loaded.XAxisTitle);
        Assert.Equal("金額", loaded.YAxisTitle);
        Assert.Equal("月份", loaded.FindAxisTitle("x"));
        Assert.Equal("金額", loaded.FindAxisTitle("y"));
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
            "月份",
            FindDescendant(loaded.ChartNode, "axis", OdfNamespaces.Chart)?
                .Children.Find(child => child.LocalName == "title" && child.NamespaceUri == OdfNamespaces.Chart)?
                .TextContent);
        Assert.Equal(
            "Sheet1.A1:A3",
            FindDescendant(loaded.ChartNode, "series", OdfNamespaces.Chart)?.GetAttribute("values-cell-range-address", OdfNamespaces.Chart));
    }

    /// <summary>
    /// 驗證 ODF 公式文件可用語意 token 建立 MathML 並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadFormulaWithMathMlRoot()
    {
        using var formula = OdfFormulaDocument.Create();
        formula.SetMathRow(
            OdfMathToken.Identifier("x"),
            OdfMathToken.Operator("="),
            OdfMathToken.Number("1"));

        using OdfFormulaDocument loaded = RoundTrip(formula, "formula.odf", OdfFormulaDocument.Load);
        OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, OdfComplianceProfiles.OasisOdf14Extended, "formula.odf");

        Assert.Equal("application/vnd.oasis.opendocument.formula", loaded.Package.MimeType);
        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal("math", loaded.MathNode.LocalName);
        Assert.Equal("http://www.w3.org/1998/Math/MathML", loaded.MathNode.NamespaceUri);
        Assert.Equal("x=1", loaded.MathText);
        Assert.Equal(3, loaded.MathTokens.Count);
        Assert.Equal(OdfMathTokenKind.Identifier, loaded.MathTokens[0].Kind);
        Assert.Equal("x", loaded.MathTokens[0].Text);
        Assert.Equal(OdfMathTokenKind.Operator, loaded.MathTokens[1].Kind);
        Assert.Equal("=", loaded.MathTokens[1].Text);
        Assert.Equal(OdfMathTokenKind.Number, loaded.MathTokens[2].Kind);
        Assert.Equal("1", loaded.MathTokens[2].Text);
        Assert.Equal("x", FindDescendant(loaded.MathNode, "mi", "http://www.w3.org/1998/Math/MathML")?.TextContent);
        Assert.Equal("=", FindDescendant(loaded.MathNode, "mo", "http://www.w3.org/1998/Math/MathML")?.TextContent);
        Assert.Equal("1", FindDescendant(loaded.MathNode, "mn", "http://www.w3.org/1998/Math/MathML")?.TextContent);
    }

    /// <summary>
    /// 驗證 ODI 影像文件可嵌入圖片並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadImageWithEmbeddedPicture()
    {
        using var image = OdfImageDocument.Create();
        byte[] bytes = CreatePngBytes();
        image.SetImageLayout(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(6),
            OdfLength.FromCentimeters(4),
            "ProductImage",
            "產品照片",
            "一張用於型錄的產品照片。");
        string href = image.SetImage(bytes, "TinyPng.png");

        using OdfImageDocument loaded = RoundTrip(image, "image.odi", OdfImageDocument.Load);
        OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, OdfComplianceProfiles.OasisOdf14Extended, "image.odi");

        Assert.Equal("application/vnd.oasis.opendocument.image", loaded.Package.MimeType);
        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal(href, loaded.ImageHref);
        Assert.NotNull(loaded.ImageInfo);
        Assert.Equal(href, loaded.ImageInfo.Path);
        Assert.Equal("image/png", loaded.ImageInfo.MediaType);
        Assert.Equal(bytes.Length, loaded.ImageInfo.Size);
        Assert.Equal(bytes, loaded.GetImageBytes());
        Assert.Equal("ProductImage", loaded.FrameName);
        Assert.Equal("產品照片", loaded.FrameTitle);
        Assert.Equal("一張用於型錄的產品照片。", loaded.FrameDescription);
        Assert.Equal(OdfLength.FromCentimeters(1), loaded.FrameX);
        Assert.Equal(OdfLength.FromCentimeters(2), loaded.FrameY);
        Assert.Equal(OdfLength.FromCentimeters(6), loaded.FrameWidth);
        Assert.Equal(OdfLength.FromCentimeters(4), loaded.FrameHeight);
        Assert.Equal(
            "6cm",
            FindDescendant(loaded.ImageNode, "frame", OdfNamespaces.Draw)?.GetAttribute("width", OdfNamespaces.Svg));
        Assert.Equal("產品照片", FindDescendant(loaded.ImageNode, "title", OdfNamespaces.Svg)?.TextContent);
        Assert.Equal("一張用於型錄的產品照片。", FindDescendant(loaded.ImageNode, "desc", OdfNamespaces.Svg)?.TextContent);
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
        database.AddDataSourceSetting("AppendTableAliasName", OdfDatabaseDataSourceSettingType.Boolean, "true");
        database.AddDataSourceSetting(
            "SuppressVersionColumns",
            OdfDatabaseDataSourceSettingType.String,
            isList: true,
            "ROWVERSION",
            "TIMESTAMP");
        database.AddDataSourceSetting("ScratchSetting", OdfDatabaseDataSourceSettingType.String, "scratch");
        database.AddTable("Customers", "SELECT * FROM Customers");
        database.AddTable("Scratch", "SELECT 1");
        database.AddQuery(
            "ActiveCustomers",
            "SELECT * FROM Customers WHERE IsActive = TRUE",
            "Active customers",
            "只列出啟用中的客戶。",
            escapeProcessing: true);
        database.AddQuery("ScratchQuery", "SELECT 1");
        Assert.True(database.RemoveTable("Scratch"));
        Assert.True(database.RemoveQuery("ScratchQuery"));
        Assert.True(database.RemoveDataSourceSetting("ScratchSetting"));

        using OdfDatabaseDocument loaded = RoundTrip(database, "database.odb", OdfDatabaseDocument.Load);
        OdfValidationReport report = OdfPackageValidator.Validate(loaded.Package, OdfComplianceProfiles.OasisOdf14Extended, "database.odb");

        Assert.Equal("application/vnd.oasis.opendocument.base", loaded.Package.MimeType);
        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal("sdbc:embedded:hsqldb", loaded.ConnectionHref);
        Assert.Equal(2, loaded.DataSourceSettings.Count);
        Assert.Equal("AppendTableAliasName", loaded.DataSourceSettings[0].Name);
        Assert.Equal(OdfDatabaseDataSourceSettingType.Boolean, loaded.DataSourceSettings[0].Type);
        Assert.False(loaded.DataSourceSettings[0].IsList);
        Assert.Equal(["true"], loaded.DataSourceSettings[0].Values);
        Assert.Equal("SuppressVersionColumns", loaded.FindDataSourceSetting("SuppressVersionColumns")?.Name);
        Assert.Equal(OdfDatabaseDataSourceSettingType.String, loaded.FindDataSourceSetting("SuppressVersionColumns")?.Type);
        Assert.True(loaded.FindDataSourceSetting("SuppressVersionColumns")?.IsList);
        Assert.Equal(["ROWVERSION", "TIMESTAMP"], loaded.FindDataSourceSetting("SuppressVersionColumns")?.Values);
        Assert.Null(loaded.FindDataSourceSetting("ScratchSetting"));
        Assert.Single(loaded.Tables);
        Assert.Equal("Customers", loaded.Tables[0].Name);
        Assert.Equal("SELECT * FROM Customers", loaded.FindTable("Customers")?.Command);
        Assert.Null(loaded.FindTable("Scratch"));
        Assert.Single(loaded.Queries);
        Assert.Equal("ActiveCustomers", loaded.Queries[0].Name);
        Assert.Equal("SELECT * FROM Customers WHERE IsActive = TRUE", loaded.FindQuery("ActiveCustomers")?.Command);
        Assert.Equal("Active customers", loaded.FindQuery("ActiveCustomers")?.Title);
        Assert.Equal("只列出啟用中的客戶。", loaded.FindQuery("ActiveCustomers")?.Description);
        Assert.True(loaded.FindQuery("ActiveCustomers")?.EscapeProcessing);
        Assert.Null(loaded.FindQuery("ScratchQuery"));
    }

    /// <summary>
    /// 驗證主控文字文件可列舉外部子文件參照並 round-trip。
    /// </summary>
    [Fact]
    public void TextMasterDocumentEnumeratesSubDocumentReferences()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapters/chapter2.odt");

        using var stream = new MemoryStream();
        master.SaveToStream(stream);
        stream.Position = 0;

        using TextMasterDocument loaded = TextMasterDocument.Load(stream, "master.odm");
        IReadOnlyList<OdfSubDocumentReference> references = loaded.GetSubDocumentReferences();

        Assert.Equal(2, references.Count);
        Assert.Equal("Chapter1", references[0].SectionName);
        Assert.Equal("chapter1.odt", references[0].Href);
        Assert.Equal("Chapter2", references[1].SectionName);
        Assert.Equal("chapters/chapter2.odt", references[1].Href);
    }

    /// <summary>
    /// 驗證基底 typed 載入入口不會接受變體格式。
    /// </summary>
    [Fact]
    public void BaseTypedLoadRejectsVariantDocumentKinds()
    {
        using var stream = new MemoryStream();
        using (OdfDocument template = OdfDocument.Create(OdfDocumentKind.TextTemplate))
        {
            template.SaveToStream(stream);
        }

        stream.Position = 0;
        Assert.Throws<InvalidOperationException>(() => TextDocument.Load(stream, "template.ott"));
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

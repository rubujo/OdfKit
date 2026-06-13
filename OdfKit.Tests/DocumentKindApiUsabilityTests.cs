using System;
using System.IO;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Image;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Chart、Formula、Image 與 Database 文件的最小高階入口。
/// </summary>
public class DocumentKindApiUsabilityTests
{
    /// <summary>
    /// 驗證 ODC 圖表可建立標題、圖例與序列佔位並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadChartWithTitleLegendAndSeries()
    {
        using var chart = OdfChartDocument.Create();
        chart.ChartClass = "bar";
        chart.ChartTitle = "營收";
        chart.SetLegend("top");
        chart.AddSeries("Sheet1.A1:A3", "Sheet1.A1");

        using OdfChartDocument loaded = RoundTrip(chart, "chart.odc", OdfChartDocument.Load);

        Assert.Equal("application/vnd.oasis.opendocument.chart", loaded.Package.MimeType);
        Assert.Equal("bar", loaded.ChartClass);
        Assert.Equal("營收", loaded.ChartTitle);
        Assert.NotNull(FindDescendant(loaded.ChartNode, "legend", OdfNamespaces.Chart));
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
        const string databaseNamespace = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";

        Assert.Equal("application/vnd.oasis.opendocument.database", loaded.Package.MimeType);
        Assert.Equal(
            "sdbc:embedded:hsqldb",
            FindDescendant(loaded.DatabaseNode, "connection-resource", databaseNamespace)?.GetAttribute("href", OdfNamespaces.XLink));
        Assert.Equal(
            "Customers",
            FindDescendant(loaded.DatabaseNode, "table-representation", databaseNamespace)?.GetAttribute("name", databaseNamespace));
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

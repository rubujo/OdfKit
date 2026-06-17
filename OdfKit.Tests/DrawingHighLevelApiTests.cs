using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定繪圖文件高階 API 的整合測試。
/// </summary>
public class DrawingHighLevelApiTests
{
    /// <summary>
    /// 驗證新增路徑 (AddPath) API 的建立與 XML 結構。
    /// </summary>
    [Fact]
    public void AddPathApiWorksCorrectly()
    {
        using var document = DrawingDocument.Create();

        var pathData = "M 10 10 L 20 20 Z";
        var shape = document.AddPath(
            pathData,
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("10.0cm"),
            OdfLength.Parse("5.0cm"));

        Assert.NotNull(shape);
        Assert.Equal("path", shape.Node.LocalName);
        Assert.Equal("1cm", shape.Node.GetAttribute("x", OdfNamespaces.Svg));
        Assert.Equal("2cm", shape.Node.GetAttribute("y", OdfNamespaces.Svg));
        Assert.Equal("10cm", shape.Node.GetAttribute("width", OdfNamespaces.Svg));
        Assert.Equal("5cm", shape.Node.GetAttribute("height", OdfNamespaces.Svg));
        Assert.Equal(pathData, shape.Node.GetAttribute("d", OdfNamespaces.Svg));
        Assert.Equal("0 0 1000 1000", shape.Node.GetAttribute("viewBox", OdfNamespaces.Svg));
    }

    /// <summary>
    /// 驗證新增多邊形 (AddPolygon) API 的邊界盒計算與坐標對齊。
    /// </summary>
    [Fact]
    public void AddPolygonApiWorksCorrectly()
    {
        using var document = DrawingDocument.Create();

        // 定義一組點集合 (2cm,2cm), (12cm,2cm), (7cm,7cm)
        // 邊界盒計算結果應為: x=2cm, y=2cm, width=10cm, height=5cm
        var points = new[]
        {
            (OdfLength.Parse("2.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("12.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("7.0cm"), OdfLength.Parse("7.0cm"))
        };

        var shape = document.AddPolygon(points);
        Assert.NotNull(shape);
        Assert.Equal("polygon", shape.Node.LocalName);

        // 驗證自動計算的 x, y, width, height (OdfLength 轉換成 points 相符)
        var xLen = OdfLength.Parse(shape.Node.GetAttribute("x", OdfNamespaces.Svg));
        var yLen = OdfLength.Parse(shape.Node.GetAttribute("y", OdfNamespaces.Svg));
        var wLen = OdfLength.Parse(shape.Node.GetAttribute("width", OdfNamespaces.Svg));
        var hLen = OdfLength.Parse(shape.Node.GetAttribute("height", OdfNamespaces.Svg));

        Assert.Equal(OdfLength.Parse("2.0cm").ToPoints(), xLen.ToPoints(), 0.001);
        Assert.Equal(OdfLength.Parse("2.0cm").ToPoints(), yLen.ToPoints(), 0.001);
        Assert.Equal(OdfLength.Parse("10.0cm").ToPoints(), wLen.ToPoints(), 0.001);
        Assert.Equal(OdfLength.Parse("5.0cm").ToPoints(), hLen.ToPoints(), 0.001);

        // 驗證相對坐標 points 屬性與 viewBox
        var viewBox = shape.Node.GetAttribute("viewBox", OdfNamespaces.Svg);
        Assert.NotNull(viewBox);

        var pointsStr = shape.Node.GetAttribute("points", OdfNamespaces.Draw);
        Assert.NotNull(pointsStr);

        // points 應該是以空白分隔的頂點座標對
        var ptParts = pointsStr.Split(' ');
        Assert.Equal(3, ptParts.Length);

        // 第一個頂點為相對 (0,0) 位置
        Assert.Equal("0,0", ptParts[0]);
    }

    /// <summary>
    /// 驗證新增連接線 (AddConnector) API 的起終點連結與幾何類型設定。
    /// </summary>
    [Fact]
    public void AddConnectorApiWorksCorrectly()
    {
        using var document = DrawingDocument.Create();
        var page = document.AddPage();

        // 建立起點與終點圖形
        var startShape = page.AddShape(OdfShapeType.Rectangle, OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("3cm"), OdfLength.Parse("2cm"));
        var endShape = page.AddShape(OdfShapeType.Rectangle, OdfLength.Parse("10cm"), OdfLength.Parse("1cm"), OdfLength.Parse("3cm"), OdfLength.Parse("2cm"));

        // 新增連接線連接兩者，指定類型為 Curve
        var connector = document.AddConnector(startShape.Id, endShape.Id, OdfConnectorType.Curve);
        Assert.NotNull(connector);
        Assert.Equal("connector", connector.Node.LocalName);
        Assert.Equal(startShape.Id, connector.Node.GetAttribute("start-shape", OdfNamespaces.Draw));
        Assert.Equal(endShape.Id, connector.Node.GetAttribute("end-shape", OdfNamespaces.Draw));
        Assert.Equal("curve", connector.Node.GetAttribute("type", OdfNamespaces.Draw));
    }

    /// <summary>
    /// 驗證新增自定義圖形 (AddCustomShape) API 的建立與幾何節點結構。
    /// </summary>
    [Fact]
    public void AddCustomShapeApiWorksCorrectly()
    {
        using var document = DrawingDocument.Create();

        var shape = document.AddCustomShape(
            "smiley",
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("5.0cm"),
            OdfLength.Parse("5.0cm"));

        Assert.NotNull(shape);
        Assert.Equal("custom-shape", shape.Node.LocalName);

        var geometryNode = shape.Node.Children.FirstOrDefault(c => c.LocalName == "enhanced-geometry" && c.NamespaceUri == OdfNamespaces.Draw);
        Assert.NotNull(geometryNode);
        Assert.Equal("smiley", geometryNode.GetAttribute("type", OdfNamespaces.Draw));
    }
}

using System;
using System.Collections.Generic;
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
    /// 驗證 <see cref="OdfDrawPage.GetPaths"/> 可讀回已建立的路徑圖形。
    /// </summary>
    [Fact]
    public void GetPaths_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("路徑頁");
        document.AddPath(
            "M 10 10 L 20 20 Z",
            OdfLength.Parse("1.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("10.0cm"),
            OdfLength.Parse("5.0cm"));

        IReadOnlyList<OdfPathInfo> paths = page.GetPaths();
        Assert.Single(paths);
        OdfPathInfo path = paths[0];
        Assert.Equal("路徑頁", path.PageName);
        Assert.Equal("M 10 10 L 20 20 Z", path.SvgPathData);
        Assert.True(path.TryGetX(out OdfLength x));
        Assert.Equal(OdfLength.Parse("1cm").ToPoints(), x.ToPoints(), 0.001);

        Assert.Single(document.GetPaths());
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetConnectors"/> 可讀回圖形連結連接線。
    /// </summary>
    [Fact]
    public void GetConnectors_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        OdfShape startShape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("3cm"),
            OdfLength.Parse("2cm"));
        OdfShape endShape = page.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.Parse("10cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("3cm"),
            OdfLength.Parse("2cm"));

        document.AddConnector(startShape.Id, endShape.Id, OdfConnectorType.Curve);

        IReadOnlyList<OdfConnectorInfo> connectors = page.GetConnectors();
        Assert.Single(connectors);
        OdfConnectorInfo connector = connectors[0];
        Assert.True(connector.IsShapeLinked);
        Assert.Equal(startShape.Id, connector.StartShapeId);
        Assert.Equal(endShape.Id, connector.EndShapeId);
        Assert.Equal(OdfConnectorType.Curve, connector.ConnectorType);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetPolygons"/> 可讀回已建立的多邊形圖形。
    /// </summary>
    [Fact]
    public void GetPolygons_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        document.AddPolygon(
        [
            (OdfLength.Parse("2.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("12.0cm"), OdfLength.Parse("2.0cm")),
            (OdfLength.Parse("7.0cm"), OdfLength.Parse("7.0cm")),
        ]);

        IReadOnlyList<OdfPolygonInfo> polygons = page.GetPolygons();
        Assert.Single(polygons);
        OdfPolygonInfo polygon = polygons[0];
        Assert.Contains("0,0", polygon.Points);
        Assert.True(polygon.TryGetX(out OdfLength x));
        Assert.Equal(OdfLength.Parse("2cm").ToPoints(), x.ToPoints(), 0.001);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetCustomShapes"/> 可讀回自定義幾何圖形。
    /// </summary>
    [Fact]
    public void GetCustomShapes_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage();
        document.AddCustomShape(
            "smiley",
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("2.0cm"),
            OdfLength.Parse("5.0cm"),
            OdfLength.Parse("5.0cm"));

        IReadOnlyList<OdfCustomShapeInfo> shapes = page.GetCustomShapes();
        Assert.Single(shapes);
        OdfCustomShapeInfo shape = shapes[0];
        Assert.Equal("smiley", shape.GeometryType);
        Assert.True(shape.TryGetWidth(out OdfLength width));
        Assert.Equal(OdfLength.Parse("5cm").ToPoints(), width.ToPoints(), 0.001);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetGroups"/> 可讀回已建立的群組圖形。
    /// </summary>
    [Fact]
    public void GetGroups_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("群組頁");
        OdfDrawGroup group = page.AddGroup("流程群組");
        group.AddShape(
            OdfShapeType.Rectangle,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("3cm"),
            OdfLength.Parse("2cm"));

        IReadOnlyList<OdfGroupInfo> groups = page.GetGroups();
        Assert.Single(groups);
        OdfGroupInfo info = groups[0];
        Assert.Equal("群組頁", info.PageName);
        Assert.Equal("流程群組", info.Name);
        Assert.False(string.IsNullOrEmpty(info.Id));

        Assert.Single(document.GetGroups());
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetLayers"/> 可讀回繪圖頁面圖層定義。
    /// </summary>
    [Fact]
    public void GetLayers_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("圖層頁");

        var layerSet = OdfNodeFactory.CreateElement("layer-set", OdfNamespaces.Draw, "draw");
        var layerNode = OdfNodeFactory.CreateElement("layer", OdfNamespaces.Draw, "draw");
        layerNode.SetAttribute("name", OdfNamespaces.Draw, "背景", "draw");
        layerNode.SetAttribute("display", OdfNamespaces.Draw, "screen", "draw");
        layerNode.SetAttribute("protected", OdfNamespaces.Draw, "true", "draw");
        layerSet.AppendChild(layerNode);
        page.Node.AppendChild(layerSet);

        IReadOnlyList<OdfLayerInfo> layers = page.GetLayers();
        Assert.Single(layers);
        OdfLayerInfo layer = layers[0];
        Assert.Equal("圖層頁", layer.PageName);
        Assert.Equal("背景", layer.Name);
        Assert.True(layer.IsProtected);
        Assert.Equal("screen", layer.Display);

        Assert.Single(document.GetLayers());
    }

    /// <summary>
    /// 驗證 <see cref="OdfDrawPage.GetTextBoxes"/> 可讀回已建立的文字方塊。
    /// </summary>
    [Fact]
    public void GetTextBoxes_RoundTripsAfterAdd()
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("標籤頁");
        page.AddTextBox(
            OdfLength.Parse("1cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("8cm"),
            OdfLength.Parse("3cm"),
            "流程圖標籤");

        IReadOnlyList<OdfDrawTextBoxInfo> textBoxes = page.GetTextBoxes();
        Assert.Single(textBoxes);
        OdfDrawTextBoxInfo info = textBoxes[0];
        Assert.Equal("標籤頁", info.PageName);
        Assert.Equal("流程圖標籤", info.Text);
        Assert.True(info.TryGetWidth(out OdfLength width));
        Assert.Equal(OdfLength.Parse("8cm").ToPoints(), width.ToPoints(), 0.001);

        Assert.Single(document.GetTextBoxes());
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    #region Drawing Object Creation

    /// <summary>
    /// Adds a text box on the drawing page.
    /// 在繪圖頁面上新增文字方塊。
    /// </summary>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <returns>The newly added text box instance. / 新增的文字方塊執行個體。</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        var textBoxNode = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        var pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, Document);
    }

    /// <summary>
    /// Adds a shape on the drawing page.
    /// 在繪圖頁面上新增圖形。
    /// </summary>
    /// <param name="shapeType">The shape type. / 圖形類型。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <returns>The newly added shape instance. / 新增的圖形執行個體。</returns>
    public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        string localName = shapeType switch
        {
            OdfShapeType.Rectangle => "rect",
            OdfShapeType.Ellipse => "ellipse",
            _ => "custom-shape"
        };

        var shapeNode = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// Adds a line segment on the drawing page.
    /// 在繪圖頁面上新增線段。
    /// </summary>
    /// <param name="x1">The start X-axis position. / 起點 X 軸座標位置。</param>
    /// <param name="y1">The start Y-axis position. / 起點 Y 軸座標位置。</param>
    /// <param name="x2">The end X-axis position. / 終點 X 軸座標位置。</param>
    /// <param name="y2">The end Y-axis position. / 終點 Y 軸座標位置。</param>
    /// <returns>The newly added line shape instance. / 新增的線段圖形執行個體。</returns>
    public OdfShape AddLine(OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var lineNode = CreateLineLikeNode("line", x1, y1, x2, y2);
        Node.AppendChild(lineNode);
        return new OdfShape(lineNode, Document);
    }

    /// <summary>
    /// Adds a connector on the drawing page.
    /// 在繪圖頁面上新增連接線。
    /// </summary>
    /// <param name="x1">The start X-axis position. / 起點 X 軸座標位置。</param>
    /// <param name="y1">The start Y-axis position. / 起點 Y 軸座標位置。</param>
    /// <param name="x2">The end X-axis position. / 終點 X 軸座標位置。</param>
    /// <param name="y2">The end Y-axis position. / 終點 Y 軸座標位置。</param>
    /// <returns>The newly added connector shape instance. / 新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var connectorNode = CreateLineLikeNode("connector", x1, y1, x2, y2);
        connectorNode.SetAttribute("type", OdfNamespaces.Draw, "standard", "draw");
        Node.AppendChild(connectorNode);
        return new OdfShape(connectorNode, Document);
    }

    /// <summary>
    /// Adds a polyline shape on the drawing page.
    /// 在繪圖頁面上新增折線圖形。
    /// </summary>
    /// <param name="points">The collection of point coordinates. / 點座標集合。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <returns>The newly added polyline shape instance. / 新增的折線圖形執行個體。</returns>
    public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var shapeNode = OdfNodeFactory.CreateElement("polyline", OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        var pointsStr = string.Join(" ", points.Select(p => $"{p.X.ToString(CultureInfo.InvariantCulture)},{p.Y.ToString(CultureInfo.InvariantCulture)}"));
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// Adds a picture on the drawing page.
    /// 在繪圖頁面上新增圖片。
    /// </summary>
    /// <param name="imageBytes">The image byte array. / 圖片的位元組陣列。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <returns>The newly added picture shape instance. / 新增的圖片圖形執行個體。</returns>
    public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = CreateDrawingFrame(x, y, w, h);

        OdfMediaManager mediaManager = new(Document.Package);
        string imageHref = mediaManager.AddImage(imageBytes, "drawing_image.png");

        var imgNode = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
        imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frame.AppendChild(imgNode);
        Node.AppendChild(frame);
        return new OdfPicture(frame, Document);
    }

    /// <summary>
    /// Adds a path shape on the drawing page.
    /// 在繪圖頁面上新增路徑圖形。
    /// </summary>
    /// <param name="svgPathData">The SVG path data description string. / SVG path data 路徑描述字串。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The width. / 寬度。</param>
    /// <param name="height">The height. / 高度。</param>
    /// <returns>The newly added path shape instance. / 新增的路徑圖形執行個體。</returns>
    public OdfShape AddPath(string svgPathData, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (svgPathData is null)
            throw new ArgumentNullException(nameof(svgPathData));

        var shapeNode = OdfNodeFactory.CreateElement("path", OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        shapeNode.SetAttribute("d", OdfNamespaces.Svg, svgPathData, "svg");
        shapeNode.SetAttribute("viewBox", OdfNamespaces.Svg, ComputePathDataViewBox(svgPathData), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 依據 SVG path data 內所有座標的邊界範圍計算 <c>svg:viewBox</c>。
    /// </summary>
    /// <remarks>
    /// ODF/ODG 的 <c>draw:path</c> 形狀實際呈現範圍是 <c>d</c> 路徑資料在 <c>viewBox</c> 座標空間中所佔的範圍，
    /// 再依 <c>viewBox</c> 到 <c>svg:width</c>/<c>svg:height</c> 的線性比例縮放。
    /// 若 <c>viewBox</c> 與路徑資料實際座標範圍不一致，圖形會被錯誤縮放（例如僅佔邊界盒一小部分），
    /// 這項落差在 LibreOffice 等真實渲染器中才會顯現，OdfKit 內部的結構性 round-trip 測試無法偵測。
    /// </remarks>
    private static string ComputePathDataViewBox(string svgPathData)
    {
        const string fallback = "0 0 1000 1000";
        if (string.IsNullOrWhiteSpace(svgPathData))
            return fallback;

        byte[] utf8PathData = Encoding.UTF8.GetBytes(svgPathData);
        if (!OdfSvgPathDataParser.TryGetBounds(utf8PathData, out OdfSvgPathBounds bounds))
            return fallback;

        double vbWidth = bounds.Width;
        double vbHeight = bounds.Height;
        if (vbWidth <= 0)
            vbWidth = 1000;
        if (vbHeight <= 0)
            vbHeight = 1000;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3}",
            Math.Floor(bounds.MinX),
            Math.Floor(bounds.MinY),
            Math.Ceiling(vbWidth),
            Math.Ceiling(vbHeight));
    }

    /// <summary>
    /// Adds a polygon shape on the drawing page.
    /// 在繪圖頁面上新增多邊形圖形。
    /// </summary>
    /// <param name="points">The length-based coordinate collection of polygon vertices. / 多邊形頂點的長度座標集合。</param>
    /// <returns>The newly added polygon shape instance. / 新增的多邊形圖形執行個體。</returns>
    public OdfShape AddPolygon(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        var ptList = points.ToList();
        if (ptList.Count == 0)
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawPage_VertexCannotBeEmpty"), nameof(points));

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;

        foreach (var p in ptList)
        {
            double px = p.X.ToPoints();
            double py = p.Y.ToPoints();
            if (px < minX)
                minX = px;
            if (px > maxX)
                maxX = px;
            if (py < minY)
                minY = py;
            if (py > maxY)
                maxY = py;
        }

        double widthPoints = maxX - minX;
        double heightPoints = maxY - minY;

        var x = OdfLength.FromPoints(minX);
        var y = OdfLength.FromPoints(minY);
        var w = OdfLength.FromPoints(widthPoints);
        var h = OdfLength.FromPoints(heightPoints);

        var shapeNode = OdfNodeFactory.CreateElement("polygon", OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        int vbWidth = (int)Math.Round(widthPoints);
        int vbHeight = (int)Math.Round(heightPoints);
        if (vbWidth <= 0)
            vbWidth = 1000;
        if (vbHeight <= 0)
            vbHeight = 1000;

        shapeNode.SetAttribute("viewBox", OdfNamespaces.Svg, $"0 0 {vbWidth.ToString(CultureInfo.InvariantCulture)} {vbHeight.ToString(CultureInfo.InvariantCulture)}", "svg");

        var relPoints = ptList.Select(p =>
        {
            double rx = widthPoints > 0 ? (p.X.ToPoints() - minX) / widthPoints * vbWidth : 0;
            double ry = heightPoints > 0 ? (p.Y.ToPoints() - minY) / heightPoints * vbHeight : 0;
            return $"{Math.Round(rx).ToString(CultureInfo.InvariantCulture)},{Math.Round(ry).ToString(CultureInfo.InvariantCulture)}";
        });

        string pointsStr = string.Join(" ", relPoints);
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// Creates a connector linking a start shape and an end shape on the drawing page.
    /// 在繪圖頁面上建立起點與終點相連的連接線。
    /// </summary>
    /// <param name="startShapeId">The start shape identifier. / 起點圖形識別碼。</param>
    /// <param name="endShapeId">The end shape identifier. / 終點圖形識別碼。</param>
    /// <param name="connectorType">The connector geometry type. / 連接線幾何類型。</param>
    /// <returns>The newly added connector shape instance. / 新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType = OdfConnectorType.Standard)
    {
        if (string.IsNullOrEmpty(startShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawPage_StartingCannotBeEmpty"), nameof(startShapeId));
        if (string.IsNullOrEmpty(endShapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawPage_EndCannotBeEmpty"), nameof(endShapeId));

        var connectorNode = OdfNodeFactory.CreateElement("connector", OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        connectorNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        connectorNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        connectorNode.SetAttribute("start-shape", OdfNamespaces.Draw, startShapeId, "draw");
        connectorNode.SetAttribute("end-shape", OdfNamespaces.Draw, endShapeId, "draw");

        string typeVal = connectorType switch
        {
            OdfConnectorType.Lines => "lines",
            OdfConnectorType.Straight => "straight",
            OdfConnectorType.Curve => "curve",
            _ => "standard"
        };
        connectorNode.SetAttribute("type", OdfNamespaces.Draw, typeVal, "draw");

        Node.AppendChild(connectorNode);
        return new OdfShape(connectorNode, Document);
    }

    /// <summary>
    /// Adds a custom shape on the drawing page.
    /// 在繪圖頁面上新增自定義幾何圖形。
    /// </summary>
    /// <param name="shapeType">The geometry type of the custom shape. / 自定義幾何圖形的幾何類型。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The width. / 寬度。</param>
    /// <param name="height">The height. / 高度。</param>
    /// <returns>The newly added custom shape instance. / 新增的自定義幾何圖形執行個體。</returns>
    public OdfShape AddCustomShape(string shapeType, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (string.IsNullOrEmpty(shapeType))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDrawPage_GeometryTypeNullable"), nameof(shapeType));

        var shapeNode = OdfNodeFactory.CreateElement("custom-shape", OdfNamespaces.Draw, "draw");
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");

        var geometryNode = OdfNodeFactory.CreateElement("enhanced-geometry", OdfNamespaces.Draw, "draw");

        // 真實 LibreOffice 並不會單憑 draw:type 字串自動補齊 enhanced-path／viewBox／equation 等幾何資料，
        // 缺少這些資料時形狀會完全不渲染（無任何錯誤訊息）。已以 LibreOffice UNO API 親手建立同名 preset
        // 取得權威幾何資料；目前僅 "smiley" 有完整對照表並補上正確資料，其餘名稱維持原始僅含 draw:type
        // 的最小骨架（呼叫端可自行後續補上 enhanced-path 等屬性，例如供應商自訂幾何或測試情境）。
        if (PresetGeometries.TryGetValue(shapeType, out PresetGeometryData? preset))
        {
            geometryNode.SetAttribute("viewBox", OdfNamespaces.Svg, preset.ViewBox, "svg");
            geometryNode.SetAttribute("glue-points", OdfNamespaces.Draw, preset.GluePoints, "draw");
            geometryNode.SetAttribute("text-areas", OdfNamespaces.Draw, preset.TextAreas, "draw");
            geometryNode.SetAttribute("type", OdfNamespaces.Draw, shapeType, "draw");
            geometryNode.SetAttribute("modifiers", OdfNamespaces.Draw, preset.Modifiers, "draw");
            geometryNode.SetAttribute("enhanced-path", OdfNamespaces.Draw, preset.EnhancedPath, "draw");
            foreach ((string name, string formula) in preset.Equations)
            {
                var equationNode = OdfNodeFactory.CreateElement("equation", OdfNamespaces.Draw, "draw");
                equationNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
                equationNode.SetAttribute("formula", OdfNamespaces.Draw, formula, "draw");
                geometryNode.AppendChild(equationNode);
            }
        }
        else
        {
            geometryNode.SetAttribute("type", OdfNamespaces.Draw, shapeType, "draw");
        }
        shapeNode.AppendChild(geometryNode);

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    private sealed record PresetGeometryData(
        string ViewBox, string GluePoints, string TextAreas, string Modifiers, string EnhancedPath,
        (string Name, string Formula)[] Equations);

    // 權威來源：以 LibreOffice 26.x 透過 com.sun.star.drawing.CustomShape／CustomShapeGeometry UNO API
    // 親手建立同名 preset 後存成 .odg 並反向比對 content.xml 取得的真實節點資料，非憑記憶或猜測。
    private static readonly Dictionary<string, PresetGeometryData> PresetGeometries = new()
    {
        ["smiley"] = new PresetGeometryData(
            ViewBox: "0 0 21600 21600",
            GluePoints: "10800 0 3163 3163 0 10800 3163 18437 10800 21600 18437 18437 21600 10800 18437 3163",
            TextAreas: "3163 3163 18437 18437",
            Modifiers: "18520",
            EnhancedPath: "U 10800 10800 10800 10800 0 360 Z N U 7305 7515 1000 1865 0 360 Z N U 14295 7515 1000 1865 0 360 Z N M 4870 ?f1 C 8680 ?f2 12920 ?f2 16730 ?f1 F N",
            Equations: new[]
            {
                ("f0", "$0 -14510"),
                ("f1", "18520-?f0 "),
                ("f2", "14510+?f0 "),
            }),
    };

    /// <summary>
    /// Adds a group on the drawing page.
    /// 在繪圖頁面上新增群組。
    /// </summary>
    /// <param name="name">The optional group name. / 選用的群組名稱。</param>
    /// <returns>The newly added drawing group instance. / 新增的繪圖群組執行個體。</returns>
    public OdfDrawGroup AddGroup(string? name = null)
    {
        var groupNode = OdfNodeFactory.CreateElement("g", OdfNamespaces.Draw, "draw");
        var id = "grp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        groupNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        groupNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        if (!string.IsNullOrWhiteSpace(name))
        {
            groupNode.SetAttribute("name", OdfNamespaces.Draw, name!, "draw");
        }

        Node.AppendChild(groupNode);
        return new OdfDrawGroup(groupNode, Document);
    }

    #endregion
}

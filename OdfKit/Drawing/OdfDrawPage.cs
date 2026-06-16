using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示 ODF 繪圖頁面（Drawing Page）的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的繪圖文件執行個體</param>
public class OdfDrawPage(OdfNode node, DrawingDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的繪圖文件。
    /// </summary>
    public DrawingDocument Document { get; } = doc;

    /// <summary>
    /// 取得或設定繪圖頁面的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定繪圖頁面所使用的母片名稱。
    /// </summary>
    public string? MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw);
        set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 取得繪圖頁面上的文字方塊清單。
    /// </summary>
    public IReadOnlyList<OdfTextBox> TextBoxes => FindDrawingObjects(
        node => ContainsDescendant(node, "text-box", OdfNamespaces.Draw),
        node => new OdfTextBox(node, Document));

    /// <summary>
    /// 取得繪圖頁面上的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfPicture> Pictures => FindDrawingObjects(
        node => ContainsDescendant(node, "image", OdfNamespaces.Draw),
        node => new OdfPicture(node, Document));

    /// <summary>
    /// 取得繪圖頁面上的一般圖形清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            node.LocalName is "rect" or "ellipse" or "custom-shape" or "line" or "connector" or "polyline" or "g",
        node => new OdfShape(node, Document));

    /// <summary>
    /// 在繪圖頁面上新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="text">文字內容</param>
    /// <returns>新增的文字方塊執行個體</returns>
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
    /// 在繪圖頁面上新增圖形。
    /// </summary>
    /// <param name="shapeType">圖形類型</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的圖形執行個體</returns>
    public OdfShape AddShape(OdfShapeType shapeType, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        string localName = shapeType switch
        {
            OdfShapeType.Rectangle => "rect",
            OdfShapeType.Ellipse => "ellipse",
            _ => "custom-shape"
        };

        var shapeNode = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增線段。
    /// </summary>
    /// <param name="x1">起點 X 軸座標位置。</param>
    /// <param name="y1">起點 Y 軸座標位置。</param>
    /// <param name="x2">終點 X 軸座標位置。</param>
    /// <param name="y2">終點 Y 軸座標位置。</param>
    /// <returns>新增的線段圖形執行個體。</returns>
    public OdfShape AddLine(OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var lineNode = CreateLineLikeNode("line", x1, y1, x2, y2);
        Node.AppendChild(lineNode);
        return new OdfShape(lineNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增連接線。
    /// </summary>
    /// <param name="x1">起點 X 軸座標位置。</param>
    /// <param name="y1">起點 Y 軸座標位置。</param>
    /// <param name="x2">終點 X 軸座標位置。</param>
    /// <param name="y2">終點 Y 軸座標位置。</param>
    /// <returns>新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var connectorNode = CreateLineLikeNode("connector", x1, y1, x2, y2);
        connectorNode.SetAttribute("type", OdfNamespaces.Draw, "standard", "draw");
        Node.AppendChild(connectorNode);
        return new OdfShape(connectorNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增折線圖形。
    /// </summary>
    /// <param name="points">點座標集合</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的折線圖形執行個體</returns>
    public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var shapeNode = OdfNodeFactory.CreateElement("polyline", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        var pointsStr = string.Join(" ", points.Select(p => $"{p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增圖片。
    /// </summary>
    /// <param name="imageBytes">圖片的位元組陣列。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="w">寬度。</param>
    /// <param name="h">高度。</param>
    /// <returns>新增的圖片圖形執行個體。</returns>
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
    /// 在繪圖頁面上新增路徑圖形。
    /// </summary>
    /// <param name="svgPathData">SVG path data 路徑描述字串</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    /// <returns>新增的路徑圖形執行個體</returns>
    public OdfShape AddPath(string svgPathData, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (svgPathData is null)
            throw new ArgumentNullException(nameof(svgPathData));

        var shapeNode = OdfNodeFactory.CreateElement("path", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        shapeNode.SetAttribute("d", OdfNamespaces.Svg, svgPathData, "svg");
        shapeNode.SetAttribute("viewBox", OdfNamespaces.Svg, "0 0 1000 1000", "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增多邊形圖形。
    /// </summary>
    /// <param name="points">多邊形頂點的長度座標集合</param>
    /// <returns>新增的多邊形圖形執行個體</returns>
    public OdfShape AddPolygon(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        var ptList = points.ToList();
        if (ptList.Count == 0)
            throw new ArgumentException("頂點集合不可為空。", nameof(points));

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
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
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

        shapeNode.SetAttribute("viewBox", OdfNamespaces.Svg, $"0 0 {vbWidth} {vbHeight}", "svg");

        var relPoints = ptList.Select(p =>
        {
            double rx = widthPoints > 0 ? (p.X.ToPoints() - minX) / widthPoints * vbWidth : 0;
            double ry = heightPoints > 0 ? (p.Y.ToPoints() - minY) / heightPoints * vbHeight : 0;
            return $"{Math.Round(rx).ToString(System.Globalization.CultureInfo.InvariantCulture)},{Math.Round(ry).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        });

        string pointsStr = string.Join(" ", relPoints);
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上建立起點與終點相連的連接線。
    /// </summary>
    /// <param name="startShapeId">起點圖形識別碼</param>
    /// <param name="endShapeId">終點圖形識別碼</param>
    /// <param name="connectorType">連接線幾何類型</param>
    /// <returns>新增的連接線圖形執行個體</returns>
    public OdfShape AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType = OdfConnectorType.Standard)
    {
        if (string.IsNullOrEmpty(startShapeId))
            throw new ArgumentException("起點圖形識別碼不可為空。", nameof(startShapeId));
        if (string.IsNullOrEmpty(endShapeId))
            throw new ArgumentException("終點圖形識別碼不可為空。", nameof(endShapeId));

        var connectorNode = OdfNodeFactory.CreateElement("connector", OdfNamespaces.Draw, "draw");
        connectorNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
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
    /// 在繪圖頁面上新增自定義幾何圖形。
    /// </summary>
    /// <param name="shapeType">自定義幾何圖形的幾何類型</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    /// <returns>新增的自定義幾何圖形執行個體</returns>
    public OdfShape AddCustomShape(string shapeType, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (string.IsNullOrEmpty(shapeType))
            throw new ArgumentException("幾何類型不可為空。", nameof(shapeType));

        var shapeNode = OdfNodeFactory.CreateElement("custom-shape", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");

        var geometryNode = OdfNodeFactory.CreateElement("enhanced-geometry", OdfNamespaces.Draw, "draw");
        geometryNode.SetAttribute("type", OdfNamespaces.Draw, shapeType, "draw");
        shapeNode.AppendChild(geometryNode);

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, Document);
    }

    /// <summary>
    /// 在繪圖頁面上新增群組。
    /// </summary>
    /// <param name="name">選用的群組名稱。</param>
    /// <returns>新增的繪圖群組執行個體。</returns>
    public OdfDrawGroup AddGroup(string? name = null)
    {
        var groupNode = OdfNodeFactory.CreateElement("g", OdfNamespaces.Draw, "draw");
        groupNode.SetAttribute("id", OdfNamespaces.Draw, "grp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        if (!string.IsNullOrWhiteSpace(name))
        {
            groupNode.SetAttribute("name", OdfNamespaces.Draw, name!, "draw");
        }

        Node.AppendChild(groupNode);
        return new OdfDrawGroup(groupNode, Document);
    }

    private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");
        return frame;
    }

    private static OdfNode CreateLineLikeNode(string localName, OdfLength x1, OdfLength y1, OdfLength x2, OdfLength y2)
    {
        var node = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Draw, "draw");
        node.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        node.SetAttribute("x1", OdfNamespaces.Svg, x1.ToString(), "svg");
        node.SetAttribute("y1", OdfNamespaces.Svg, y1.ToString(), "svg");
        node.SetAttribute("x2", OdfNamespaces.Svg, x2.ToString(), "svg");
        node.SetAttribute("y2", OdfNamespaces.Svg, y2.ToString(), "svg");
        return node;
    }

    private IReadOnlyList<T> FindDrawingObjects<T>(Func<OdfNode, bool> predicate, Func<OdfNode, T> factory)
    {
        List<T> objects = [];
        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && predicate(child))
            {
                objects.Add(factory(child));
            }
        }

        return objects.AsReadOnly();
    }

    private static bool ContainsDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return true;
            }

            if (ContainsDescendant(child, localName, namespaceUri))
            {
                return true;
            }
        }

        return false;
    }
}


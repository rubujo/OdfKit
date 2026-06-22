using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    #region Drawing Object Creation

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
        var id = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
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

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        bool hasCoordinate = false;

        // 依目前指令字母決定每組參數中座標軸（X／Y）的排列方式，
        // 而非天真假設所有指令的參數皆以 X、Y 交替排列
        // （例如 H／V 僅有單一軸，A 的 7 個參數中只有最後 2 個才是端點座標）。
        char command = 'M';
        int paramIndexInGroup = 0;

        int index = 0;
        int length = svgPathData.Length;
        while (index < length)
        {
            char c = svgPathData[index];
            if (char.IsLetter(c))
            {
                command = c;
                paramIndexInGroup = 0;
                index++;
                continue;
            }

            if (char.IsDigit(c) || c is '-' or '+' or '.')
            {
                int start = index;
                index++;
                while (index < length && (char.IsDigit(svgPathData[index]) || svgPathData[index] is '.' or 'e' or 'E' ||
                    ((svgPathData[index] is '-' or '+') && (svgPathData[index - 1] is 'e' or 'E'))))
                {
                    index++;
                }

                string token = svgPathData.Substring(start, index - start);
                if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
                {
                    char upperCommand = char.ToUpperInvariant(command);
                    int groupSize = upperCommand switch
                    {
                        'H' or 'V' => 1,
                        'A' => 7,
                        'C' => 6,
                        'S' or 'Q' => 4,
                        _ => 2 // M、L、T 與未知指令：成對的 (x, y)
                    };

                    // 在群組內判斷目前參數屬於哪個座標軸；A 指令僅最後兩個參數（索引 5、6）為 (x, y)
                    bool isXCoordinate = upperCommand switch
                    {
                        'H' => true,
                        'V' => false,
                        'A' => paramIndexInGroup == 5,
                        _ => paramIndexInGroup % 2 == 0
                    };
                    bool isYCoordinate = upperCommand switch
                    {
                        'H' => false,
                        'V' => true,
                        'A' => paramIndexInGroup == 6,
                        _ => paramIndexInGroup % 2 == 1
                    };

                    if (isXCoordinate || isYCoordinate)
                    {
                        hasCoordinate = true;
                        if (isXCoordinate)
                        {
                            if (value < minX)
                                minX = value;
                            if (value > maxX)
                                maxX = value;
                        }
                        else
                        {
                            if (value < minY)
                                minY = value;
                            if (value > maxY)
                                maxY = value;
                        }
                    }

                    paramIndexInGroup = (paramIndexInGroup + 1) % groupSize;
                }

                continue;
            }

            index++;
        }

        if (!hasCoordinate)
            return fallback;

        double vbWidth = maxX - minX;
        double vbHeight = maxY - minY;
        if (vbWidth <= 0)
            vbWidth = 1000;
        if (vbHeight <= 0)
            vbHeight = 1000;

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0} {1} {2} {3}",
            Math.Floor(minX),
            Math.Floor(minY),
            Math.Ceiling(vbWidth),
            Math.Ceiling(vbHeight));
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
    private static readonly System.Collections.Generic.Dictionary<string, PresetGeometryData> PresetGeometries = new()
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
    /// 在繪圖頁面上新增群組。
    /// </summary>
    /// <param name="name">選用的群組名稱。</param>
    /// <returns>新增的繪圖群組執行個體。</returns>
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

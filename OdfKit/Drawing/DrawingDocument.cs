using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Presentation;

namespace OdfKit.Drawing;

/// <summary>
/// 表示 ODF 繪圖文件（Drawing Document）的類別。
/// </summary>
public class DrawingDocument : OdfDocument
{
    private readonly List<OdfDrawPage> _pages = [];

    /// <summary>
    /// 取得繪圖頁面的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfDrawPage> Pages => _pages.AsReadOnly();

    /// <summary>
    /// 初始化 <see cref="DrawingDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件執行個體</param>
    public DrawingDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.graphics");
        }
        ParsePages();
    }

    private void ParsePages()
    {
        _pages.Clear();
        var drawingNode = GetDrawingNode();
        foreach (var child in drawingNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "page" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                _pages.Add(new OdfDrawPage(child, this));
            }
        }
    }

    /// <summary>
    /// 取得繪圖節點。
    /// </summary>
    /// <returns>繪圖的 <see cref="OdfNode"/> 節點</returns>
    public OdfNode GetDrawingNode()
    {
        var body = FindChildElement(ContentRoot, "body", OdfNamespaces.Office);
        if (body is null)
        {
            body = new OdfNode(OdfNodeType.Element, "body", OdfNamespaces.Office, "office");
            ContentRoot.AppendChild(body);
        }

        var drawing = FindChildElement(body, "drawing", OdfNamespaces.Office);
        if (drawing is null)
        {
            drawing = new OdfNode(OdfNodeType.Element, "drawing", OdfNamespaces.Office, "office");
            body.AppendChild(drawing);
        }

        return drawing;
    }

    /// <summary>
    /// 新增一個繪圖頁面。
    /// </summary>
    /// <param name="name">頁面名稱</param>
    /// <returns>新增的繪圖頁面執行個體</returns>
    public OdfDrawPage AddPage(string? name = null)
    {
        var drawingNode = GetDrawingNode();
        var pageNode = OdfNodeFactory.CreateElement("page", OdfNamespaces.Draw, "draw");
        
        string pageName = name ?? $"Page {_pages.Count + 1}";
        pageNode.SetAttribute("name", OdfNamespaces.Draw, pageName, "draw");

        drawingNode.AppendChild(pageNode);
        var page = new OdfDrawPage(pageNode, this);
        _pages.Add(page);
        return page;
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>預設的內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:body>" +
            "<office:drawing />" +
            "</office:body>" +
            "</office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>預設的樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:styles></office:styles>" +
            "<office:automatic-styles></office:automatic-styles>" +
            "<office:master-styles></office:master-styles>" +
            "</office:document-styles>";
    }

    /// <summary>
    /// 合併來源文件的內容節點至本文件中。
    /// </summary>
    /// <param name="sourceDoc">來源 ODF 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">樣式名稱重映射字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcDraw = sourceDoc as DrawingDocument ?? throw new ArgumentException("Source document must be a DrawingDocument.");
        var destDrawNode = GetDrawingNode();
        var srcDrawNode = srcDraw.GetDrawingNode();
        
        foreach (var child in srcDrawNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcDraw.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                destDrawNode.AppendChild(imported);
            }
        }
        ParsePages();
    }

    /// <summary>
    /// 尋找子元素。
    /// </summary>
    /// <param name="parent">父節點</param>
    /// <param name="localName">元素的區域名稱</param>
    /// <param name="nsUri">命名空間 URI</param>
    /// <returns>找到的子元素節點，若無則為 <c>null</c></returns>
    public OdfNode? FindChildElement(OdfNode parent, string localName, string nsUri)
    {
        foreach (var child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element && 
                string.Equals(child.LocalName, localName, StringComparison.Ordinal) && 
                string.Equals(child.NamespaceUri, nsUri, StringComparison.Ordinal))
            {
                return child;
            }
        }
        return null;
    }
}

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

    private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        return frame;
    }
}

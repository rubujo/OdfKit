using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
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
    private OdfDrawPageCollection? _pageCollection;

    /// <summary>
    /// 取得繪圖頁面的集合。
    /// </summary>
    public OdfDrawPageCollection Pages => _pageCollection ??= new OdfDrawPageCollection(this);

    /// <summary>
    /// 初始化 <see cref="DrawingDocument"/> 類別的新執行個體。
    /// </summary>
    public DrawingDocument() : this(OdfPackage.Create(new MemoryStream()))
    {
        Package.SetMimeType("application/vnd.oasis.opendocument.graphics");
        Package.SaveManifestToEntries();
    }

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

    /// <summary>
    /// 建立新的 ODG 繪圖文件。
    /// </summary>
    /// <returns>新的 <see cref="DrawingDocument"/> 執行個體。</returns>
    public static DrawingDocument Create()
    {
        return (DrawingDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Graphics);
    }

    /// <summary>
    /// 從指定路徑載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="path">ODG 文件路徑。</param>
    /// <returns>載入完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODG 繪圖時擲出。</exception>
    public new static DrawingDocument Load(string path)
    {
        return EnsureDrawing(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="stream">包含 ODG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODG 繪圖時擲出。</exception>
    public new static DrawingDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureDrawing(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    internal IReadOnlyList<OdfDrawPage> GetPagesSnapshot()
    {
        return _pages.AsReadOnly();
    }

    private static DrawingDocument EnsureDrawing(OdfDocument document)
    {
        if (document is DrawingDocument drawing)
        {
            return drawing;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODG 繪圖。");
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
    /// 在繪圖文件上新增路徑圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="svgPathData">SVG path data 路徑描述字串</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    /// <returns>新增的路徑圖形執行個體</returns>
    public OdfShape AddPath(string svgPathData, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (Pages.Count == 0) AddPage();
        return Pages[0].AddPath(svgPathData, x, y, width, height);
    }

    /// <summary>
    /// 在繪圖文件上新增多邊形圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="points">多邊形頂點的長度座標集合</param>
    /// <returns>新增的多邊形圖形執行個體</returns>
    public OdfShape AddPolygon(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (Pages.Count == 0) AddPage();
        return Pages[0].AddPolygon(points);
    }

    /// <summary>
    /// 在繪圖文件上建立起點與終點相連的連接線（套用至預設第一頁）。
    /// </summary>
    /// <param name="startShapeId">起點圖形識別碼</param>
    /// <param name="endShapeId">終點圖形識別碼</param>
    /// <param name="connectorType">連接線幾何類型</param>
    /// <returns>新增的連接線圖形執行個體</returns>
    public OdfShape AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType = OdfConnectorType.Standard)
    {
        if (Pages.Count == 0) AddPage();
        return Pages[0].AddConnector(startShapeId, endShapeId, connectorType);
    }

    /// <summary>
    /// 在繪圖文件上新增自定義幾何圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="shapeType">自定義形狀的類型名稱</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    /// <returns>新增的自定義幾何圖形執行個體</returns>
    public OdfShape AddCustomShape(string shapeType, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (Pages.Count == 0) AddPage();
        return Pages[0].AddCustomShape(shapeType, x, y, width, height);
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
/// 提供繪圖頁面的索引、列舉與新增入口。
/// </summary>
public sealed class OdfDrawPageCollection : IReadOnlyList<OdfDrawPage>
{
    private readonly DrawingDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfDrawPageCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬繪圖文件。</param>
    public OdfDrawPageCollection(DrawingDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得繪圖頁面數量。
    /// </summary>
    public int Count => _document.GetPagesSnapshot().Count;

    /// <summary>
    /// 依索引取得繪圖頁面。
    /// </summary>
    /// <param name="index">以 0 為基準的頁面索引。</param>
    /// <returns>指定的繪圖頁面。</returns>
    public OdfDrawPage this[int index] => _document.GetPagesSnapshot()[index];

    /// <summary>
    /// 新增繪圖頁面。
    /// </summary>
    /// <param name="name">選用的頁面名稱。</param>
    /// <returns>新增完成的繪圖頁面。</returns>
    public OdfDrawPage Add(string? name = null)
    {
        return _document.AddPage(name);
    }

    /// <summary>
    /// 取得繪圖頁面列舉器。
    /// </summary>
    /// <returns>繪圖頁面列舉器。</returns>
    public IEnumerator<OdfDrawPage> GetEnumerator()
    {
        return _document.GetPagesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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
        if (svgPathData is null) throw new ArgumentNullException(nameof(svgPathData));

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
        if (points is null) throw new ArgumentNullException(nameof(points));
        var ptList = points.ToList();
        if (ptList.Count == 0) throw new ArgumentException("頂點集合不可為空。", nameof(points));

        double minX = double.MaxValue;
        double maxX = double.MinValue;
        double minY = double.MaxValue;
        double maxY = double.MinValue;

        foreach (var p in ptList)
        {
            double px = p.X.ToPoints();
            double py = p.Y.ToPoints();
            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
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
        if (vbWidth <= 0) vbWidth = 1000;
        if (vbHeight <= 0) vbHeight = 1000;

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
        if (string.IsNullOrEmpty(startShapeId)) throw new ArgumentException("起點圖形識別碼不可為空。", nameof(startShapeId));
        if (string.IsNullOrEmpty(endShapeId)) throw new ArgumentException("終點圖形識別碼不可為空。", nameof(endShapeId));

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
        if (string.IsNullOrEmpty(shapeType)) throw new ArgumentException("幾何類型不可為空。", nameof(shapeType));

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

/// <summary>
/// 表示 ODF 繪圖群組。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">所屬的 ODF 文件執行個體。</param>
public sealed class OdfDrawGroup(OdfNode node, OdfDocument doc) : OdfShape(node, doc)
{
    /// <summary>
    /// 取得或設定群組名稱。
    /// </summary>
    public string? Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw);
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 在群組內新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="w">寬度。</param>
    /// <param name="h">高度。</param>
    /// <param name="text">文字內容。</param>
    /// <returns>新增的文字方塊執行個體。</returns>
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
    /// 在群組內新增圖形。
    /// </summary>
    /// <param name="shapeType">圖形類型。</param>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="w">寬度。</param>
    /// <param name="h">高度。</param>
    /// <returns>新增的圖形執行個體。</returns>
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

    private static OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
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
}

/// <summary>
/// 表示連接線幾何類型的列舉。
/// </summary>
public enum OdfConnectorType
{
    /// <summary>
    /// 標準折線連接線。
    /// </summary>
    Standard,

    /// <summary>
    /// 多段折線連接線。
    /// </summary>
    Lines,

    /// <summary>
    /// 直線連接線。
    /// </summary>
    Straight,

    /// <summary>
    /// 曲線連接線。
    /// </summary>
    Curve
}

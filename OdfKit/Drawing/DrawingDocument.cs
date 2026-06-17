using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示 ODF 繪圖文件（Drawing Document）的類別。
/// </summary>
public partial class DrawingDocument : OdfDocument
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
    /// 非同步從指定路徑載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="path">ODG 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="DrawingDocument"/>。</returns>
    public new static async Task<DrawingDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureDrawing(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

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

    /// <summary>
    /// 非同步從指定資料流載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="stream">包含 ODG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="DrawingDocument"/>。</returns>
    public new static async Task<DrawingDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureDrawing(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    internal IReadOnlyList<OdfDrawPage> GetPagesSnapshot()
    {
        return _pages.AsReadOnly();
    }

    private static DrawingDocument EnsureDrawing(OdfDocument document)
    {
        if (document is DrawingDocument drawing && document.DocumentKind == OdfDocumentKind.Graphics)
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
        if (Pages.Count == 0)
            AddPage();
        return Pages[0].AddPath(svgPathData, x, y, width, height);
    }

    /// <summary>
    /// 在繪圖文件上新增多邊形圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="points">多邊形頂點的長度座標集合</param>
    /// <returns>新增的多邊形圖形執行個體</returns>
    public OdfShape AddPolygon(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (Pages.Count == 0)
            AddPage();
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
        if (Pages.Count == 0)
            AddPage();
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
        if (Pages.Count == 0)
            AddPage();
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


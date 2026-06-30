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
/// Represents an ODF drawing document.
/// 表示 ODF 繪圖文件（Drawing Document）的類別。
/// </summary>
public partial class DrawingDocument : OdfDocument
{
    private readonly List<OdfDrawPage> _pages = [];
    private OdfDrawPageCollection? _pageCollection;

    /// <summary>
    /// Gets the collection of drawing pages.
    /// 取得繪圖頁面的集合。
    /// </summary>
    public OdfDrawPageCollection Pages => _pageCollection ??= new OdfDrawPageCollection(this);

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawingDocument"/> class.
    /// 初始化 <see cref="DrawingDocument"/> 類別的新執行個體。
    /// </summary>
    public DrawingDocument() : this(OdfPackage.Create(new MemoryStream()))
    {
        Package.SetMimeType("application/vnd.oasis.opendocument.graphics");
        Package.SaveManifestToEntries();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawingDocument"/> class.
    /// 初始化 <see cref="DrawingDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / Odf 套件執行個體。</param>
    public DrawingDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.graphics");
        }
        ParsePages();
    }

    /// <summary>
    /// Creates a new ODG drawing document fluent builder.
    /// 建立新的 ODG 繪圖文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="DrawingDocumentBuilder"/> instance. / 新的 <see cref="DrawingDocumentBuilder"/> 執行個體。</returns>
    public static DrawingDocumentBuilder Builder()
    {
        return new DrawingDocumentBuilder(Create());
    }

    /// <summary>
    /// Creates a new ODG drawing document.
    /// 建立新的 ODG 繪圖文件。
    /// </summary>
    /// <returns>A new <see cref="DrawingDocument"/> instance. / 新的 <see cref="DrawingDocument"/> 執行個體。</returns>
    public static DrawingDocument Create()
    {
        return (DrawingDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Graphics);
    }

    /// <summary>
    /// Creates a new drawing document from the specified graphics template document.
    /// 從指定的繪圖範本文件建立新的繪圖文件。
    /// </summary>
    /// <param name="template">The graphics template document. / 繪圖範本文件。</param>
    /// <param name="clearUserContent">Whether to clear the text content of each page in the template while keeping layout and shape structure. / 是否清除範本中各頁面的文字內容，但保留版面配置與形狀結構。</param>
    /// <returns>The created <see cref="DrawingDocument"/> instance. / 建立完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    public static DrawingDocument CreateFromTemplate(GraphicsTemplateDocument template, bool clearUserContent = false)
    {
        return (DrawingDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Graphics, "application/vnd.oasis.opendocument.graphics", clearUserContent);
    }

    /// <summary>
    /// Creates an equivalent ODG (ZIP package) drawing document from a FODG flat XML drawing document, with identical content.
    /// 從 FODG 扁平 XML 繪圖文件建立等價的 ODG（ZIP 封裝）繪圖文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODG flat XML drawing document. / 來源 FODG 扁平 XML 繪圖文件。</param>
    /// <returns>The created <see cref="DrawingDocument"/> instance. / 建立完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    public static DrawingDocument CreateFromFlatDocument(FlatGraphicsDocument document) =>
        (DrawingDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Graphics, targetIsFlatXml: false);

    /// <inheritdoc/>
    protected override void ClearTemplateUserContent()
    {
        ClearParagraphTextContentRecursive(GetDrawingNode());
    }

    /// <summary>
    /// Loads an ODG drawing document from the specified path.
    /// 從指定路徑載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="path">The ODG document path. / ODG 文件路徑。</param>
    /// <returns>The loaded <see cref="DrawingDocument"/> instance. / 載入完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODG drawing. / 當指定文件不是 ODG 繪圖時擲出。</exception>
    public new static DrawingDocument Load(string path) =>
        OdfDocumentVariantSupport.Load<DrawingDocument>(path, OdfDocumentKind.Graphics, "Err_DrawingDocument_SpecifiedOdfFileOdg");

    /// <summary>
    /// Asynchronously loads an ODG drawing document from the specified path.
    /// 非同步從指定路徑載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="path">The ODG document path. / ODG 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DrawingDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DrawingDocument"/>。</returns>
    public new static Task<DrawingDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<DrawingDocument>(path, OdfDocumentKind.Graphics, "Err_DrawingDocument_SpecifiedOdfFileOdg", cancellationToken);

    /// <summary>
    /// Loads an ODG drawing document from the specified stream.
    /// 從指定資料流載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODG document content. / 包含 ODG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="DrawingDocument"/> instance. / 載入完成的 <see cref="DrawingDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODG drawing. / 當指定文件不是 ODG 繪圖時擲出。</exception>
    public new static DrawingDocument Load(Stream stream, string? fileName = null) =>
        OdfDocumentVariantSupport.Load<DrawingDocument>(stream, OdfDocumentKind.Graphics, "Err_DrawingDocument_SpecifiedOdfFileOdg", fileName);

    /// <summary>
    /// Asynchronously loads an ODG drawing document from the specified stream.
    /// 非同步從指定資料流載入 ODG 繪圖文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODG document content. / 包含 ODG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DrawingDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DrawingDocument"/>。</returns>
    public new static Task<DrawingDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<DrawingDocument>(stream, OdfDocumentKind.Graphics, "Err_DrawingDocument_SpecifiedOdfFileOdg", fileName, cancellationToken);

    internal IReadOnlyList<OdfDrawPage> GetPagesSnapshot()
    {
        return _pages.AsReadOnly();
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
    /// Gets the drawing node.
    /// 取得繪圖節點。
    /// </summary>
    /// <returns>The drawing's <see cref="OdfNode"/> node. / 繪圖的 <see cref="OdfNode"/> 節點。</returns>
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
    /// Adds a drawing page.
    /// 新增一個繪圖頁面。
    /// </summary>
    /// <param name="name">The page name. / 頁面名稱。</param>
    /// <returns>The newly added drawing page instance. / 新增的繪圖頁面執行個體。</returns>
    public OdfDrawPage AddPage(string? name = null)
    {
        var drawingNode = GetDrawingNode();
        var pageNode = OdfNodeFactory.CreateElement("page", OdfNamespaces.Draw, "draw");

        string pageName = name ?? $"Page {_pages.Count + 1}";
        pageNode.SetAttribute("name", OdfNamespaces.Draw, pageName, "draw");
        pageNode.SetAttribute("master-page-name", OdfNamespaces.Draw, "Default", "draw");
        pageNode.SetAttribute("style-name", OdfNamespaces.Draw, "dp1", "draw");

        drawingNode.AppendChild(pageNode);
        var page = new OdfDrawPage(pageNode, this);
        _pages.Add(page);
        return page;
    }

    /// <summary>
    /// Adds a path shape to the drawing document (applied to the default first page).
    /// 在繪圖文件上新增路徑圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="svgPathData">The SVG path data description string. / SVG path data 路徑描述字串。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The width. / 寬度。</param>
    /// <param name="height">The height. / 高度。</param>
    /// <returns>The newly added path shape instance. / 新增的路徑圖形執行個體。</returns>
    public OdfShape AddPath(string svgPathData, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (Pages.Count == 0)
            AddPage();
        return Pages[0].AddPath(svgPathData, x, y, width, height);
    }

    /// <summary>
    /// Adds a polygon shape to the drawing document (applied to the default first page).
    /// 在繪圖文件上新增多邊形圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="points">The length-based coordinate collection of polygon vertices. / 多邊形頂點的長度座標集合。</param>
    /// <returns>The newly added polygon shape instance. / 新增的多邊形圖形執行個體。</returns>
    public OdfShape AddPolygon(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (Pages.Count == 0)
            AddPage();
        return Pages[0].AddPolygon(points);
    }

    /// <summary>
    /// Creates a connector linking a start shape and an end shape on the drawing document (applied to the default first page).
    /// 在繪圖文件上建立起點與終點相連的連接線（套用至預設第一頁）。
    /// </summary>
    /// <param name="startShapeId">The start shape identifier. / 起點圖形識別碼。</param>
    /// <param name="endShapeId">The end shape identifier. / 終點圖形識別碼。</param>
    /// <param name="connectorType">The connector geometry type. / 連接線幾何類型。</param>
    /// <returns>The newly added connector shape instance. / 新增的連接線圖形執行個體。</returns>
    public OdfShape AddConnector(string startShapeId, string endShapeId, OdfConnectorType connectorType = OdfConnectorType.Standard)
    {
        if (Pages.Count == 0)
            AddPage();
        return Pages[0].AddConnector(startShapeId, endShapeId, connectorType);
    }

    /// <summary>
    /// Adds a custom shape to the drawing document (applied to the default first page).
    /// 在繪圖文件上新增自定義幾何圖形（套用至預設第一頁）。
    /// </summary>
    /// <param name="shapeType">The custom shape type name. / 自定義形狀的類型名稱。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The width. / 寬度。</param>
    /// <param name="height">The height. / 高度。</param>
    /// <returns>The newly added custom shape instance. / 新增的自定義幾何圖形執行個體。</returns>
    public OdfShape AddCustomShape(string shapeType, OdfLength x, OdfLength y, OdfLength width, OdfLength height)
    {
        if (Pages.Count == 0)
            AddPage();
        return Pages[0].AddCustomShape(shapeType, x, y, width, height);
    }

    /// <summary>
    /// Gets the default content XML string.
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>The default content XML string. / 預設的內容 XML 字串。</returns>
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
    /// Gets the default styles XML string.
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>The default styles XML string. / 預設的樣式 XML 字串。</returns>
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
    /// Merges the content nodes of a source document into this document.
    /// 合併來源文件的內容節點至本文件中。
    /// </summary>
    /// <param name="sourceDoc">The source ODF document. / 來源 ODF 文件。</param>
    /// <param name="options">The merge options. / 合併設定選項。</param>
    /// <param name="renameMap">The dictionary mapping renamed style names. / 樣式名稱變更的對照字典。</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcDraw = sourceDoc as DrawingDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_DrawingDocument_SourceDocumentDrawingdocument"));
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
    /// Finds a child element.
    /// 尋找子元素。
    /// </summary>
    /// <param name="parent">The parent node. / 父節點。</param>
    /// <param name="localName">The local name of the element. / 元素的區域名稱。</param>
    /// <param name="nsUri">The namespace URI. / 命名空間 URI。</param>
    /// <returns>The found child element node, or <c>null</c> if none is found. / 找到的子元素節點，若無則為 <c>null</c>。</returns>
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

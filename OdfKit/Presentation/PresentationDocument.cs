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

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片頁面方向的列舉。
/// </summary>
public enum OdfPageOrientation
{
    /// <summary>
    /// 橫向。
    /// </summary>
    Landscape,

    /// <summary>
    /// 直向。
    /// </summary>
    Portrait
}

/// <summary>
/// 表示圖形種類的列舉。
/// </summary>
public enum OdfShapeType
{
    /// <summary>
    /// 矩形。
    /// </summary>
    Rectangle,

    /// <summary>
    /// 橢圓形。
    /// </summary>
    Ellipse,

    /// <summary>
    /// 自訂圖形。
    /// </summary>
    Custom
}

/// <summary>
/// 表示投影片切換效果類型的列舉。
/// </summary>
public enum OdfTransitionType
{
    /// <summary>
    /// 淡出。
    /// </summary>
    Fade,

    /// <summary>
    /// 推入。
    /// </summary>
    Push,

    /// <summary>
    /// 擦去。
    /// </summary>
    Wipe,

    /// <summary>
    /// 縮放。
    /// </summary>
    Zoom,

    /// <summary>
    /// 分割。
    /// </summary>
    Split
}

/// <summary>
/// 表示動畫效果類型的列舉。
/// </summary>
public enum OdfAnimationType
{
    /// <summary>
    /// 淡入。
    /// </summary>
    FadeIn,

    /// <summary>
    /// 淡出。
    /// </summary>
    FadeOut,

    /// <summary>
    /// 放大。
    /// </summary>
    ZoomIn,

    /// <summary>
    /// 向右擦去。
    /// </summary>
    WipeRight
}

/// <summary>
/// 表示 ODF 簡報文件（Presentation Document）的類別。
/// </summary>
public partial class PresentationDocument : OdfDocument
{
    private readonly List<OdfSlide> _slides = [];
    private OdfSlideCollection? _slideCollection;

    /// <summary>
    /// 取得投影片集合。
    /// </summary>
    public OdfSlideCollection Slides => _slideCollection ??= new OdfSlideCollection(this);

    /// <summary>
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    public PresentationDocument() : this(OdfPackage.Create(new MemoryStream()))
    {
        Package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        Package.SaveManifestToEntries();
    }

    /// <summary>
    /// 初始化 <see cref="PresentationDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件執行個體</param>
    public PresentationDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.presentation");
        }
        ParseSlides();
    }

    /// <summary>
    /// 建立新的 ODP 簡報文件。
    /// </summary>
    /// <returns>新的 <see cref="PresentationDocument"/> 執行個體。</returns>
    public static PresentationDocument Create()
    {
        return (PresentationDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Presentation);
    }

    /// <summary>
    /// 從指定路徑載入 ODP 簡報文件。
    /// </summary>
    /// <param name="path">ODP 文件路徑。</param>
    /// <returns>載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(string path)
    {
        return EnsurePresentation(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODP 簡報文件。
    /// </summary>
    /// <param name="stream">包含 ODP 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="PresentationDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODP 簡報時擲出。</exception>
    public new static PresentationDocument Load(Stream stream, string? fileName = null)
    {
        return EnsurePresentation(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    internal IReadOnlyList<OdfSlide> GetSlidesSnapshot()
    {
        return _slides.AsReadOnly();
    }

    private static PresentationDocument EnsurePresentation(OdfDocument document)
    {
        if (document is PresentationDocument presentation)
        {
            return presentation;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODP 簡報。");
    }

    private void ParseSlides()
    {
        _slides.Clear();
        var presentationNode = GetPresentationNode();
        foreach (var child in presentationNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "page" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                _slides.Add(new OdfSlide(child, this));
            }
        }
    }

    /// <summary>
    /// 取得簡報的核心節點。
    /// </summary>
    /// <returns>簡報的 <see cref="OdfNode"/> 節點</returns>
    public OdfNode GetPresentationNode()
    {
        var body = FindChildElement(ContentRoot, "body", OdfNamespaces.Office);
        if (body is null)
        {
            body = new OdfNode(OdfNodeType.Element, "body", OdfNamespaces.Office, "office");
            ContentRoot.AppendChild(body);
        }

        var presentation = FindChildElement(body, "presentation", OdfNamespaces.Office);
        if (presentation is null)
        {
            presentation = new OdfNode(OdfNodeType.Element, "presentation", OdfNamespaces.Office, "office");
            body.AppendChild(presentation);
        }

        return presentation;
    }

    /// <summary>
    /// 新增一張投影片。
    /// </summary>
    /// <param name="name">投影片的名稱</param>
    /// <returns>新增的投影片執行個體</returns>
    public OdfSlide AddSlide(string? name = null)
    {
        var presentationNode = GetPresentationNode();
        var slideNode = new OdfNode(OdfNodeType.Element, "page", OdfNamespaces.Draw, "draw");
        
        string slideName = name ?? $"Slide {_slides.Count + 1}";
        slideNode.SetAttribute("name", OdfNamespaces.Draw, slideName, "draw");
        slideNode.SetAttribute("master-page-name", OdfNamespaces.Draw, "Default", "draw");

        presentationNode.AppendChild(slideNode);
        var slide = new OdfSlide(slideNode, this);
        _slides.Add(slide);
        return slide;
    }

    /// <summary>
    /// 複製指定的投影片。
    /// </summary>
    /// <param name="sourceSlideIndex">來源投影片的索引位置</param>
    /// <returns>複製後的新投影片執行個體</returns>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public OdfSlide CloneSlide(int sourceSlideIndex)
    {
        if (sourceSlideIndex < 0 || sourceSlideIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSlideIndex));
        }

        var sourceSlide = _slides[sourceSlideIndex];
        var clonedNode = sourceSlide.Node.CloneNode(deep: true);

        string baseName = sourceSlide.Name;
        string newName = $"{baseName}_Clone";
        int count = 1;
        while (_slides.Exists(s => string.Equals(s.Name, newName, StringComparison.Ordinal)))
        {
            newName = $"{baseName}_Clone_{count++}";
        }
        clonedNode.SetAttribute("name", OdfNamespaces.Draw, newName, "draw");

        var presentationNode = GetPresentationNode();
        presentationNode.InsertAfter(clonedNode, sourceSlide.Node);

        ParseSlides();
        return _slides.Find(s => string.Equals(s.Name, newName, StringComparison.Ordinal))!;
    }

    /// <summary>
    /// 刪除指定的投影片。
    /// </summary>
    /// <param name="slideIndex">投影片的索引位置</param>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public void DeleteSlide(int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex));
        }

        var slide = _slides[slideIndex];
        var presentationNode = GetPresentationNode();
        presentationNode.RemoveChild(slide.Node);
        _slides.RemoveAt(slideIndex);
    }

    /// <summary>
    /// 移動投影片的順序位置。
    /// </summary>
    /// <param name="fromIndex">來源投影片的索引位置</param>
    /// <param name="toIndex">目標投影片的索引位置</param>
    /// <exception cref="ArgumentOutOfRangeException">索引超出範圍時拋出</exception>
    public void MoveSlide(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _slides.Count || toIndex < 0 || toIndex >= _slides.Count)
        {
            throw new ArgumentOutOfRangeException();
        }

        if (fromIndex == toIndex)
        {
            return;
        }

        var presentationNode = GetPresentationNode();
        var slideToMove = _slides[fromIndex];
        presentationNode.RemoveChild(slideToMove.Node);

        if (toIndex == _slides.Count - 1)
        {
            presentationNode.AppendChild(slideToMove.Node);
        }
        else
        {
            var refSlide = _slides[toIndex > fromIndex ? toIndex + 1 : toIndex];
            presentationNode.InsertBefore(slideToMove.Node, refSlide.Node);
        }

        ParseSlides();
    }

    /// <summary>
    /// 設定投影片的尺寸。
    /// </summary>
    /// <param name="width">寬度</param>
    /// <param name="height">高度</param>
    public void SetSlideSize(OdfLength width, OdfLength height)
    {
        var pageLayoutProps = GetDefaultPageLayoutProperties();
        pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, width.ToString(), "fo");
        pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, height.ToString(), "fo");
    }

    /// <summary>
    /// 設定投影片的方向。
    /// </summary>
    /// <param name="orientation">投影片方向列舉值</param>
    public void SetSlideOrientation(OdfPageOrientation orientation)
    {
        var pageLayoutProps = GetDefaultPageLayoutProperties();
        string orientationStr = orientation is OdfPageOrientation.Landscape ? "landscape" : "portrait";
        pageLayoutProps.SetAttribute("print-orientation", OdfNamespaces.Style, orientationStr, "style");

        string? wStr = pageLayoutProps.GetAttribute("page-width", OdfNamespaces.Fo);
        string? hStr = pageLayoutProps.GetAttribute("page-height", OdfNamespaces.Fo);

        if (!string.IsNullOrEmpty(wStr) && !string.IsNullOrEmpty(hStr))
        {
            var w = OdfLength.Parse(wStr);
            var h = OdfLength.Parse(hStr);

            if (orientation is OdfPageOrientation.Landscape && w.ToPoints() < h.ToPoints())
            {
                pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
            }
            else if (orientation is OdfPageOrientation.Portrait && w.ToPoints() > h.ToPoints())
            {
                pageLayoutProps.SetAttribute("page-width", OdfNamespaces.Fo, h.ToString(), "fo");
                pageLayoutProps.SetAttribute("page-height", OdfNamespaces.Fo, w.ToString(), "fo");
            }
        }
    }

    private OdfNode GetDefaultPageLayoutProperties()
    {
        var autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
        if (autoStyles is null)
        {
            autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
            StylesRoot.AppendChild(autoStyles);
        }

        OdfNode? layoutNode = null;
        foreach (var child in autoStyles.Children)
        {
            if (child.LocalName is "page-layout" && child.NamespaceUri == OdfNamespaces.Style)
            {
                layoutNode = child;
                break;
            }
        }

        if (layoutNode is null)
        {
            layoutNode = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
            layoutNode.SetAttribute("name", OdfNamespaces.Style, "PM1", "style");
            autoStyles.AppendChild(layoutNode);
        }

        var props = FindChildElement(layoutNode, "page-layout-properties", OdfNamespaces.Style);
        if (props is null)
        {
            props = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
            layoutNode.AppendChild(props);
        }

        return props;
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
            "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
            "xmlns:anim=\"urn:oasis:names:tc:opendocument:xmlns:animation:1.0\" " +
            "xmlns:smil=\"urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:body>" +
            "<office:presentation />" +
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
            "xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" " +
            "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:styles></office:styles>" +
            "<office:automatic-styles>" +
            "<style:page-layout style:name=\"PM1\">" +
            "<style:page-layout-properties fo:page-width=\"28cm\" fo:page-height=\"21cm\" style:print-orientation=\"landscape\"/>" +
            "</style:page-layout>" +
            "</office:automatic-styles>" +
            "<office:master-styles>" +
            "<style:master-page style:name=\"Default\" style:page-layout-name=\"PM1\"/>" +
            "</office:master-styles>" +
            "</office:document-styles>";
    }

    /// <summary>
    /// 合併來源文件的內容節點至本文件中。
    /// </summary>
    /// <param name="sourceDoc">來源 ODF 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">樣式名稱重映射字典</param>
    /// <exception cref="ArgumentException">來源文件非簡報文件時拋出</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcPres = sourceDoc as PresentationDocument ?? throw new ArgumentException("Source document must be a PresentationDocument.");
        var destPresNode = GetPresentationNode();
        var srcPresNode = srcPres.GetPresentationNode();
        
        foreach (var child in srcPresNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcPres.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                destPresNode.AppendChild(imported);
            }
        }
        ParseSlides();
    }

    /// <summary>
    /// 尋找指定的子元素節點。
    /// </summary>
    /// <param name="parent">要尋找的父節點</param>
    /// <param name="localName">區域名稱</param>
    /// <param name="nsUri">命名空間 URI</param>
    /// <returns>尋找到的子節點，若未找到則為 <c>null</c></returns>
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

    /// <summary>
    /// 新增母片（Master Page）。
    /// </summary>
    /// <param name="name">母片名稱</param>
    /// <param name="pageLayoutName">頁面版面配置名稱</param>
    /// <exception cref="ArgumentException">引數為 null 或空字串時拋出</exception>
    public void AddMasterPage(string name, string pageLayoutName)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Master page name cannot be null or empty.", nameof(name));
        }
        if (string.IsNullOrEmpty(pageLayoutName))
        {
            throw new ArgumentException("Page layout name cannot be null or empty.", nameof(pageLayoutName));
        }

        var masterStyles = FindChildElement(StylesRoot, "master-styles", OdfNamespaces.Office);
        if (masterStyles is null)
        {
            masterStyles = new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");
            StylesRoot.AppendChild(masterStyles);
        }

        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPage.SetAttribute("name", OdfNamespaces.Style, name, "style");
        masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, pageLayoutName, "style");

        masterStyles.AppendChild(masterPage);
    }

    /// <summary>
    /// 建立新的投影片版面配置（Presentation Page Layout）。
    /// </summary>
    /// <param name="name">版面配置名稱</param>
    /// <returns>新增的投影片版面配置執行個體</returns>
    public OdfPresentationPageLayout CreatePresentationPageLayout(string name)
    {
        var autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
        if (autoStyles is null)
        {
            autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
            StylesRoot.AppendChild(autoStyles);
        }
        var layoutNode = new OdfNode(OdfNodeType.Element, "presentation-page-layout", OdfNamespaces.Style, "style");
        layoutNode.SetAttribute("name", OdfNamespaces.Style, name, "style");
        autoStyles.AppendChild(layoutNode);
        return new OdfPresentationPageLayout(layoutNode);
    }

    /// <summary>
    /// 取得指定的投影片版面配置。
    /// </summary>
    /// <param name="name">版面配置名稱</param>
    /// <returns>投影片版面配置執行個體，若不存在則為 <c>null</c></returns>
    public OdfPresentationPageLayout? GetPresentationPageLayout(string name)
    {
        // 優先搜尋 ContentDom
        var autoStyles = FindChildElement(ContentRoot, "automatic-styles", OdfNamespaces.Office);
        if (autoStyles is not null)
        {
            foreach (var child in autoStyles.Children)
            {
                if (child.LocalName is "presentation-page-layout" && 
                    child.NamespaceUri == OdfNamespaces.Style && 
                    child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    return new OdfPresentationPageLayout(child);
                }
            }
        }
        // 搜尋 StylesDom
        autoStyles = FindChildElement(StylesRoot, "automatic-styles", OdfNamespaces.Office);
        if (autoStyles is not null)
        {
            foreach (var child in autoStyles.Children)
            {
                if (child.LocalName is "presentation-page-layout" && 
                    child.NamespaceUri == OdfNamespaces.Style && 
                    child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    return new OdfPresentationPageLayout(child);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 取得簡報的講義頁面（Handout Page）。
    /// </summary>
    public OdfHandoutPage HandoutPage
    {
        get
        {
            var masterStyles = FindChildElement(StylesRoot, "master-styles", OdfNamespaces.Office);
            if (masterStyles is null)
            {
                masterStyles = new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");
                StylesRoot.AppendChild(masterStyles);
            }

            var handoutNode = FindChildElement(masterStyles, "handout", OdfNamespaces.Presentation);
            if (handoutNode is null)
            {
                handoutNode = new OdfNode(OdfNodeType.Element, "handout", OdfNamespaces.Presentation, "presentation");
                handoutNode.SetAttribute("name", OdfNamespaces.Style, "DefaultHandout", "style");
                handoutNode.SetAttribute("page-layout-name", OdfNamespaces.Style, "PM1", "style");
                masterStyles.AppendChild(handoutNode);
            }
            return new OdfHandoutPage(handoutNode, this);
        }
    }
}

/// <summary>
/// 提供簡報投影片的索引、列舉與新增入口。
/// </summary>
public sealed class OdfSlideCollection : IReadOnlyList<OdfSlide>
{
    private readonly PresentationDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfSlideCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬簡報文件。</param>
    public OdfSlideCollection(PresentationDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得投影片數量。
    /// </summary>
    public int Count => _document.GetSlidesSnapshot().Count;

    /// <summary>
    /// 依索引取得投影片。
    /// </summary>
    /// <param name="index">以 0 為基準的投影片索引。</param>
    /// <returns>指定投影片。</returns>
    public OdfSlide this[int index] => _document.GetSlidesSnapshot()[index];

    /// <summary>
    /// 新增投影片。
    /// </summary>
    /// <param name="name">選用的投影片名稱。</param>
    /// <returns>新增完成的投影片。</returns>
    public OdfSlide Add(string? name = null)
    {
        return _document.AddSlide(name);
    }

    /// <summary>
    /// 取得投影片列舉器。
    /// </summary>
    /// <returns>投影片列舉器。</returns>
    public IEnumerator<OdfSlide> GetEnumerator()
    {
        return _document.GetSlidesSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 表示簡報投影片（Slide）的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的簡報文件執行個體</param>
public partial class OdfSlide(OdfNode node, PresentationDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的簡報文件。
    /// </summary>
    public PresentationDocument Document { get; } = doc;

    /// <summary>
    /// 取得或設定投影片名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定投影片使用的母片名稱。
    /// </summary>
    public string MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定投影片使用的版面配置名稱。
    /// </summary>
    public string? PresentationPageLayoutName
    {
        get => Node.GetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// 取得或設定投影片頁首使用的樣式名稱。
    /// </summary>
    public string? UseHeaderName
    {
        get => Node.GetAttribute("use-header-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-header-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-header-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// 取得或設定投影片頁尾使用的樣式名稱。
    /// </summary>
    public string? UseFooterName
    {
        get => Node.GetAttribute("use-footer-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-footer-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-footer-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// 取得或設定投影片日期與時間使用的樣式名稱。
    /// </summary>
    public string? UseDateTimeName
    {
        get => Node.GetAttribute("use-date-time-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-date-time-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-date-time-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// 取得投影片的備忘錄頁面（Notes Page）。
    /// </summary>
    public OdfNotesPage SpeakerNotesPage
    {
        get
        {
            var notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
            if (notesNode is null)
            {
                notesNode = new OdfNode(OdfNodeType.Element, "notes", OdfNamespaces.Presentation, "presentation");
                Node.AppendChild(notesNode);
            }
            return new OdfNotesPage(notesNode, this);
        }
    }

    /// <summary>
    /// 取得或設定投影片備忘錄文字。
    /// </summary>
    public string SpeakerNotes
    {
        get => SpeakerNotesPage.SpeakerNotesText;
        set => SpeakerNotesPage.SpeakerNotesText = value;
    }

    /// <summary>
    /// 取得動畫根節點。
    /// </summary>
    public OdfAnimationNode AnimationRoot
    {
        get
        {
            OdfNode? mainSeq = null;
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.LocalName is "seq" && child.NamespaceUri is "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
                {
                    string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                    if (nodeType is "main-sequence")
                    {
                        mainSeq = child;
                        break;
                    }
                }
            }
            if (mainSeq is null)
            {
                mainSeq = new OdfNode(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
                Node.AppendChild(mainSeq);
            }
            return new OdfAnimationNode(mainSeq);
        }
    }

    /// <summary>
    /// 取得投影片中所有預留位置的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholder> Placeholders
    {
        get
        {
            List<OdfPlaceholder> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    string? ph = child.GetAttribute("placeholder", OdfNamespaces.Presentation);
                    if (ph is "true")
                    {
                        list.Add(new OdfPlaceholder(child, this));
                    }
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// 取得投影片上的文字方塊清單。
    /// </summary>
    public IReadOnlyList<OdfTextBox> TextBoxes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "text-box", OdfNamespaces.Draw),
        node => new OdfTextBox(node, this));

    /// <summary>
    /// 取得投影片上的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfPicture> Pictures => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "image", OdfNamespaces.Draw),
        node => new OdfPicture(node, this));

    /// <summary>
    /// 取得投影片上的一般圖形清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            node.LocalName is "rect" or "ellipse" or "custom-shape" or "line" or "connector" or "polyline",
        node => new OdfShape(node, this));

    /// <summary>
    /// 在投影片上新增一個預留位置（Placeholder）。
    /// </summary>
    /// <param name="type">預留位置類型</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的預留位置圖形執行個體</returns>
    public OdfPlaceholder AddPlaceholder(OdfPlaceholderType type, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode shapeNode = new(OdfNodeType.Element, "rect", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        
        Node.AppendChild(shapeNode);
        var placeholder = new OdfPlaceholder(shapeNode, this)
        {
            PlaceholderType = type
        };
        return placeholder;
    }

    /// <summary>
    /// 在投影片上新增內嵌物件（如其他文件或子組件）。
    /// </summary>
    /// <param name="subPath">內嵌物件於套件內的路徑</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增內嵌物件圖形執行個體</returns>
    public OdfShape AddEmbeddedObject(string subPath, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        OdfNode objNode = new(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        
        string href = subPath;
        if (!href.StartsWith("./"))
        {
            href = "./" + href;
        }
        if (href.EndsWith("/"))
        {
            href = href.Substring(0, href.Length - 1);
        }
        
        objNode.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        objNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        
        frame.AppendChild(objNode);
        Node.AppendChild(frame);
        return new OdfShape(frame, this);
    }

    /// <summary>
    /// 在投影片上新增文字方塊。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <param name="text">文字內容</param>
    /// <returns>新增的文字方塊圖形執行個體</returns>
    public OdfTextBox AddTextBox(OdfLength x, OdfLength y, OdfLength w, OdfLength h, string text)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        OdfNode textBoxNode = new(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBoxNode);

        OdfNode pNode = new(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.TextContent = text;
        textBoxNode.AppendChild(pNode);

        Node.AppendChild(frame);
        return new OdfTextBox(frame, this);
    }

    /// <summary>
    /// 在投影片上新增基本圖形。
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

        OdfNode shapeNode = new(OdfNodeType.Element, localName, OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, this);
    }

    /// <summary>
    /// 在投影片上新增折線圖形。
    /// </summary>
    /// <param name="points">點座標集合</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的折線圖形執行個體</returns>
    public OdfShape AddPolyline(IEnumerable<System.Drawing.PointF> points, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode shapeNode = new(OdfNodeType.Element, "polyline", OdfNamespaces.Draw, "draw");
        shapeNode.SetAttribute("id", OdfNamespaces.Draw, "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        shapeNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        shapeNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        shapeNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        shapeNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");

        var pointsStr = string.Join(" ", points.Select(p => $"{p.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        shapeNode.SetAttribute("points", OdfNamespaces.Draw, pointsStr, "draw");

        Node.AppendChild(shapeNode);
        return new OdfShape(shapeNode, this);
    }

    /// <summary>
    /// 在投影片上新增圖片。
    /// </summary>
    /// <param name="imageBytes">圖片的位元組陣列</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的圖片圖形執行個體</returns>
    public OdfPicture AddPicture(byte[] imageBytes, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        var frame = CreateDrawingFrame(x, y, w, h);
        
        OdfMediaManager mediaManager = new(Document.Package);
        string imageHref = mediaManager.AddImage(imageBytes, "slide_image.png");

        OdfNode imgNode = new(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
        imgNode.SetAttribute("href", OdfNamespaces.XLink, imageHref, "xlink");
        imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        
        frame.AppendChild(imgNode);
        Node.AppendChild(frame);
        return new OdfPicture(frame, this);
    }

    /// <summary>
    /// 設定投影片切換動畫效果。
    /// </summary>
    /// <param name="type">切換效果類型</param>
    /// <param name="duration">持續時間</param>
    public void SetTransition(OdfTransitionType type, OdfLength duration)
    {
        string durStr = $"{duration.ToPoints() / 72.0:F2}s";

        switch (type)
        {
            case OdfTransitionType.Fade:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fadeOverColor", "smil");
                break;
            case OdfTransitionType.Push:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "push", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fromBottom", "smil");
                break;
            case OdfTransitionType.Wipe:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "wipe", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "leftToRight", "smil");
                break;
            case OdfTransitionType.Zoom:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "zoom", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                break;
            case OdfTransitionType.Split:
                Node.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "split", "smil");
                Node.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "horizontalOut", "smil");
                break;
        }

        Node.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        Node.SetAttribute("transition-type", OdfNamespaces.Presentation, "automatic", "presentation");
    }

    private OdfNode CreateDrawingFrame(OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode frame = new(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("id", OdfNamespaces.Draw, "frm_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        frame.SetAttribute("anchor-type", OdfNamespaces.Draw, "page", "draw");
        return frame;
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
/// 表示投影片內圖形的基底類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的文件執行個體</param>
/// <param name="slide">所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c></param>
public class OdfShape(OdfNode node, OdfDocument doc, OdfSlide? slide)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得所屬的投影片執行個體。
    /// </summary>
    public OdfSlide? Slide { get; } = slide;

    /// <summary>
    /// 取得所屬的 ODF 文件。
    /// </summary>
    public OdfDocument Document { get; } = doc;

    /// <summary>
    /// 取得圖形節點的區域名稱。
    /// </summary>
    public string LocalName => Node.LocalName;

    /// <summary>
    /// 取得或設定圖形的識別碼。
    /// </summary>
    public string Id
    {
        get => Node.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("id", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定圖形的填滿色彩。
    /// </summary>
    public string? FillColor
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "fill-color", OdfNamespaces.Draw, "graphic");
        set
        {
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "fill-color", OdfNamespaces.Draw, value ?? string.Empty, "draw");
        }
    }

    /// <summary>
    /// 取得或設定圖形的邊框色彩。
    /// </summary>
    public string? StrokeColor
    {
        get => Document.StyleEngine.GetStyleProperty(Node.GetAttribute("style-name", OdfNamespaces.Draw) ?? string.Empty, "stroke-color", OdfNamespaces.Svg, "graphic");
        set
        {
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "graphic", "graphic-properties", "stroke-color", OdfNamespaces.Svg, value ?? string.Empty, "svg");
        }
    }

    /// <summary>
    /// 初始化 <see cref="OdfShape"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="slide">所屬的投影片執行個體</param>
    public OdfShape(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide)
    {
    }

    /// <summary>
    /// 初始化 <see cref="OdfShape"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="doc">所屬的 ODF 文件執行個體</param>
    public OdfShape(OdfNode node, OdfDocument doc) : this(node, doc, null)
    {
    }

    /// <summary>
    /// 為圖形新增動畫效果。
    /// </summary>
    /// <param name="type">動畫類型</param>
    /// <param name="duration">動畫持續時間</param>
    /// <param name="delay">動畫延遲啟動時間</param>
    /// <exception cref="InvalidOperationException">若圖形不屬於投影片則拋出</exception>
    public void Animate(OdfAnimationType type, OdfLength duration, OdfLength delay)
    {
        if (Slide is null)
        {
            throw new InvalidOperationException("Animation is only supported for presentation slides.");
        }
        var slideNode = Slide.Node;
        var mainSeq = FindOrCreateAnimationSequence(slideNode);

        OdfNode stepPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        stepPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "next", "smil");
        mainSeq.AppendChild(stepPar);

        string durStr = $"{duration.ToPoints() / 72.0:F2}s";
        string delayStr = $"{delay.ToPoints() / 72.0:F2}s";
        string targetId = Id;

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = "shp_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Id = targetId;
        }

        switch (type)
        {
            case OdfAnimationType.FadeIn:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.FadeOut:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "out", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.ZoomIn:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "zoom", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
            case OdfAnimationType.WipeRight:
                {
                    OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
                    filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetId, "smil");
                    filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "wipe", "smil");
                    filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "leftToRight", "smil");
                    filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
                    filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
                    filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
                    stepPar.AppendChild(filter);
                }
                break;
        }
    }

    private static OdfNode FindOrCreateAnimationSequence(OdfNode slideNode)
    {
        foreach (var child in slideNode.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName is "seq" && child.NamespaceUri is "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
            {
                string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                if (nodeType is "main-sequence")
                {
                    return child;
                }
            }
        }

        OdfNode mainSeq = new(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
        slideNode.AppendChild(mainSeq);
        return mainSeq;
    }
}

/// <summary>
/// 表示投影片中的文字方塊圖形。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的文件執行個體</param>
/// <param name="slide">所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c></param>
public class OdfTextBox(OdfNode node, OdfDocument doc, OdfSlide? slide) : OdfShape(node, doc, slide)
{
    /// <summary>
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="slide">所屬的投影片執行個體</param>
    public OdfTextBox(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide) { }

    /// <summary>
    /// 初始化 <see cref="OdfTextBox"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="doc">所屬的 ODF 文件執行個體</param>
    public OdfTextBox(OdfNode node, OdfDocument doc) : this(node, doc, null) { }

    /// <summary>
    /// 取得文字方塊中的純文字內容。
    /// </summary>
    public string Text => FindDescendant(Node, "p", OdfNamespaces.Text)?.TextContent ?? string.Empty;

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
}

/// <summary>
/// 表示投影片中的圖片圖形。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的文件執行個體</param>
/// <param name="slide">所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c></param>
public class OdfPicture(OdfNode node, OdfDocument doc, OdfSlide? slide) : OdfShape(node, doc, slide)
{
    /// <summary>
    /// 初始化 <see cref="OdfPicture"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="slide">所屬的投影片執行個體</param>
    public OdfPicture(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide) { }

    /// <summary>
    /// 初始化 <see cref="OdfPicture"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="doc">所屬的 ODF 文件執行個體</param>
    public OdfPicture(OdfNode node, OdfDocument doc) : this(node, doc, null) { }

    /// <summary>
    /// 取得圖片在 ODF 封裝中的參照路徑。
    /// </summary>
    public string? ImageHref => FindDescendant(Node, "image", OdfNamespaces.Draw)?.GetAttribute("href", OdfNamespaces.XLink);

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
}

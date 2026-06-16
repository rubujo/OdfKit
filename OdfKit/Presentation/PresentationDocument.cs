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

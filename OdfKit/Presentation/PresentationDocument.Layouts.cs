using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    #region Presentation Layouts & Defaults

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
    /// Merges content nodes from the source document into this document.
    /// 合併來源文件的內容節點至本文件中。
    /// </summary>
    /// <param name="sourceDoc">The source ODF document. / 來源 ODF 文件。</param>
    /// <param name="options">The merge options. / 合併設定選項。</param>
    /// <param name="renameMap">The style rename map. / 樣式名稱變更的對照字典。</param>
    /// <exception cref="ArgumentException">Thrown when the source document is not a presentation document. / 來源文件非簡報文件時拋出。</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcPres = sourceDoc as PresentationDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_SourceDocumentPresentationdocument"));
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
    /// Finds a specified child element node.
    /// 尋找指定的子元素節點。
    /// </summary>
    /// <param name="parent">The parent node to search. / 要尋找的父節點。</param>
    /// <param name="localName">The local name. / 區域名稱。</param>
    /// <param name="nsUri">The namespace URI. / 命名空間 URI。</param>
    /// <returns>The found child node, or <see langword="null"/> if none is found. / 尋找到的子節點，若未找到則為 <see langword="null"/>。</returns>
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
    /// Adds a master page.
    /// 新增母片（Master Page）。
    /// </summary>
    /// <param name="name">The master page name. / 母片名稱。</param>
    /// <param name="pageLayoutName">The page layout name. / 頁面版面配置名稱。</param>
    /// <exception cref="ArgumentException">Thrown when an argument is <see langword="null"/> or an empty string. / 引數為 <see langword="null"/> 或空字串時拋出。</exception>
    public void AddMasterPage(string name, string pageLayoutName)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_MasterCannotBeEmpty"), nameof(name));
        }
        if (string.IsNullOrEmpty(pageLayoutName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_PageCannotBeEmpty"), nameof(pageLayoutName));
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
    /// Creates a presentation page layout.
    /// 建立新的投影片版面配置（Presentation Page Layout）。
    /// </summary>
    /// <param name="name">The layout name. / 版面配置名稱。</param>
    /// <returns>The added presentation page layout instance. / 新增的投影片版面配置執行個體。</returns>
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
    /// Gets the specified presentation page layout.
    /// 取得指定的投影片版面配置。
    /// </summary>
    /// <param name="name">The layout name. / 版面配置名稱。</param>
    /// <returns>The presentation page layout instance, or <see langword="null"/> if it does not exist. / 投影片版面配置執行個體，若不存在則為 <see langword="null"/>。</returns>
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
    /// Gets the presentation handout page.
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

    #endregion
}

using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件的頁面設定。
/// </summary>
public class OdfPageSetup
{
    private readonly TextDocument _doc;
    private readonly string _masterPageName;
    private readonly string _pageLayoutName;

    /// <summary>使用預設主頁面（Standard / Mpm1）初始化。</summary>
    public OdfPageSetup(TextDocument doc) : this(doc, "Standard", "Mpm1") { }

    internal OdfPageSetup(TextDocument doc, string masterPageName, string pageLayoutName)
    {
        _doc = doc;
        _masterPageName = masterPageName;
        _pageLayoutName = pageLayoutName;
    }

    private OdfNode ContentDom => _doc.ContentDom;
    private OdfNode StylesDom => _doc.StylesDom;

    internal void EnsureNodes()
    {
        _ = FindOrCreatePageLayoutProperties();
        _ = FindOrCreateMasterPage();
    }

    /// <summary>
    /// 取得或設定頁面寬度（公分）。
    /// </summary>
    public double PageWidth
    {
        get
        {
            string? val = GetPageProp("page-width");
            if (val is not null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                return d;
            return 21.0;
        }
        set => SetPageProp("page-width", $"{value}cm");
    }

    /// <summary>
    /// 取得或設定頁面高度（公分）。
    /// </summary>
    public double PageHeight
    {
        get
        {
            string? val = GetPageProp("page-height");
            if (val is not null && val.EndsWith("cm") && double.TryParse(val.Substring(0, val.Length - 2), out var d))
                return d;
            return 29.7;
        }
        set => SetPageProp("page-height", $"{value}cm");
    }

    /// <summary>
    /// 取得或設定頁面使用方式。
    /// </summary>
    public OdfPageUsage PageUsage
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return (props.GetAttribute("page-usage", OdfNamespaces.Style) ?? "all") switch
            {
                "left" => OdfPageUsage.Left,
                "right" => OdfPageUsage.Right,
                "mirrored" => OdfPageUsage.Mirrored,
                _ => OdfPageUsage.All,
            };
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            string str = value switch
            {
                OdfPageUsage.Left => "left",
                OdfPageUsage.Right => "right",
                OdfPageUsage.Mirrored => "mirrored",
                _ => "all",
            };
            props.SetAttribute("page-usage", OdfNamespaces.Style, str, "style");
        }
    }

    /// <summary>
    /// 取得或設定頁面的文字書寫模式。
    /// </summary>
    public OdfWritingMode WritingMode
    {
        get
        {
            var props = FindOrCreatePageLayoutProperties();
            return OdfWritingModeExtensions.FromOdfToken(props.GetAttribute("writing-mode", OdfNamespaces.Style));
        }
        set
        {
            var props = FindOrCreatePageLayoutProperties();
            props.SetAttribute("writing-mode", OdfNamespaces.Style, value.ToOdfToken(), "style");
        }
    }

    private string? GetPageStyleProp(string name)
    {
        var props = FindOrCreatePageLayoutProperties();
        return props.GetAttribute(name, OdfNamespaces.Style);
    }

    private void SetPageStyleProp(string name, string? val)
    {
        var props = FindOrCreatePageLayoutProperties();
        if (val is not null)
        {
            props.SetAttribute(name, OdfNamespaces.Style, val, "style");
        }
        else
        {
            props.RemoveAttribute(name, OdfNamespaces.Style);
        }
    }

    /// <summary>
    /// 取得或設定頁面版面配置網格的模式。
    /// </summary>
    public OdfLayoutGridMode LayoutGridMode
    {
        get
        {
            return (GetPageStyleProp("layout-grid-mode") ?? "none") switch
            {
                "line" => OdfLayoutGridMode.Line,
                "both" => OdfLayoutGridMode.Both,
                _ => OdfLayoutGridMode.None,
            };
        }
        set
        {
            string str = value switch
            {
                OdfLayoutGridMode.Line => "line",
                OdfLayoutGridMode.Both => "both",
                _ => "none",
            };
            SetPageStyleProp("layout-grid-mode", str);
        }
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎高度。
    /// </summary>
    public string? LayoutGridBaseHeight
    {
        get => GetPageStyleProp("layout-grid-base-height");
        set => SetPageStyleProp("layout-grid-base-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的基礎寬度。
    /// </summary>
    public string? LayoutGridBaseWidth
    {
        get => GetPageStyleProp("layout-grid-base-width");
        set => SetPageStyleProp("layout-grid-base-width", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的旁註標記（注音）高度。
    /// </summary>
    public string? LayoutGridRubyHeight
    {
        get => GetPageStyleProp("layout-grid-ruby-height");
        set => SetPageStyleProp("layout-grid-ruby-height", value);
    }

    /// <summary>
    /// 取得或設定版面配置網格的行數。
    /// </summary>
    public int? LayoutGridLines
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-lines"), out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-lines", value?.ToString());
    }

    /// <summary>
    /// 取得或設定版面配置網格的字數。
    /// </summary>
    public int? LayoutGridCharacters
    {
        get => int.TryParse(GetPageStyleProp("layout-grid-characters"), out var val) ? val : null;
        set => SetPageStyleProp("layout-grid-characters", value?.ToString());
    }

    /// <summary>
    /// 取得或設定一個值，指出是否顯示版面配置網格。
    /// </summary>
    public bool? LayoutGridDisplay
    {
        get => GetPageStyleProp("layout-grid-display") == "true" ? true : (GetPageStyleProp("layout-grid-display") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-display", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定一個值，指出是否列印版面配置網格。
    /// </summary>
    public bool? LayoutGridPrint
    {
        get => GetPageStyleProp("layout-grid-print") == "true" ? true : (GetPageStyleProp("layout-grid-print") == "false" ? false : null);
        set => SetPageStyleProp("layout-grid-print", value is null ? null : (value.Value ? "true" : "false"));
    }

    /// <summary>
    /// 取得或設定頁首的文字內容。
    /// </summary>
    public string? HeaderText
    {
        get => GetHeaderFooterText("header");
        set => SetHeaderFooterText("header", value);
    }

    /// <summary>
    /// 取得或設定左頁首的文字內容。
    /// </summary>
    public string? HeaderLeftText
    {
        get => GetHeaderFooterText("header-left");
        set => SetHeaderFooterText("header-left", value);
    }

    /// <summary>
    /// 取得或設定頁尾的文字內容。
    /// </summary>
    public string? FooterText
    {
        get => GetHeaderFooterText("footer");
        set => SetHeaderFooterText("footer", value);
    }

    /// <summary>
    /// 取得或設定左頁尾的文字內容。
    /// </summary>
    public string? FooterLeftText
    {
        get => GetHeaderFooterText("footer-left");
        set => SetHeaderFooterText("footer-left", value);
    }

    private string? GetPageProp(string name)
    {
        var props = FindOrCreatePageLayoutProperties();
        return props.GetAttribute(name, OdfNamespaces.Fo);
    }

    private void SetPageProp(string name, string val)
    {
        var props = FindOrCreatePageLayoutProperties();
        props.SetAttribute(name, OdfNamespaces.Fo, val, "fo");
    }

    private OdfNode FindOrCreatePageLayoutNode()
    {
        var autoStyles = FindOrCreateChild(_doc.StylesDom, "automatic-styles", OdfNamespaces.Office, "office");
        foreach (var child in autoStyles.Children)
        {
            if (child.LocalName == "page-layout" && child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == _pageLayoutName)
                return child;
        }
        var pageLayout = new OdfNode(OdfNodeType.Element, "page-layout", OdfNamespaces.Style, "style");
        pageLayout.SetAttribute("name", OdfNamespaces.Style, _pageLayoutName, "style");
        autoStyles.AppendChild(pageLayout);
        return pageLayout;
    }

    private OdfNode FindOrCreatePageLayoutProperties()
    {
        var layoutNode = FindOrCreatePageLayoutNode();
        foreach (var child in layoutNode.Children)
        {
            if (child.LocalName == "page-layout-properties" && child.NamespaceUri == OdfNamespaces.Style)
                return child;
        }
        var props = new OdfNode(OdfNodeType.Element, "page-layout-properties", OdfNamespaces.Style, "style");
        layoutNode.AppendChild(props);
        return props;
    }

    private OdfNode FindOrCreateMasterPage()
    {
        var masterStyles = FindOrCreateChild(_doc.StylesDom, "master-styles", OdfNamespaces.Office, "office");
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == _masterPageName)
                return child;
        }
        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPage.SetAttribute("name", OdfNamespaces.Style, _masterPageName, "style");
        masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, _pageLayoutName, "style");
        masterStyles.AppendChild(masterPage);
        return masterPage;
    }

    private string? GetHeaderFooterText(string localName)
    {
        var mp = FindOrCreateMasterPage();
        foreach (var child in mp.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                foreach (var p in child.Children)
                {
                    if (p.LocalName == "p" && p.NamespaceUri == OdfNamespaces.Text)
                    {
                        return p.TextContent;
                    }
                }
            }
        }
        return null;
    }

    private void SetHeaderFooterText(string localName, string? value)
    {
        var mp = FindOrCreateMasterPage();
        OdfNode? target = null;
        foreach (var child in mp.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                target = child;
                break;
            }
        }

        if (value is null)
        {
            if (target is not null)
                mp.RemoveChild(target);
        }
        else
        {
            if (target is null)
            {
                target = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Style, "style");
                mp.AppendChild(target);
            }

            OdfNode? pNode = null;
            foreach (var child in target.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    pNode = child;
                    break;
                }
            }
            if (pNode is null)
            {
                pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                target.AppendChild(pNode);
            }
            pNode.TextContent = value;
        }
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        if (localName == "font-face-decls" && parent.Children.Count > 0)
        {
            parent.InsertBefore(node, parent.Children[0]);
        }
        else
        {
            parent.AppendChild(node);
        }
        return node;
    }

    /// <summary>
    /// 在頁面設定中新增字型宣告項目。
    /// </summary>
    /// <param name="name">字型代碼或別名</param>
    /// <param name="fontFamily">實際的字型名稱</param>
    /// <param name="genericFamily">泛用字型系列</param>
    /// <param name="pitch">字距模式</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
    {
        void AddToDom(OdfNode domRoot)
        {
            var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
            foreach (var child in fontDecls.Children)
            {
                if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                    if (genericFamily is not null)
                        child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null)
                        child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null)
                fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null)
                fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(_doc.ContentDom);
        if (_doc.StylesDom is not null)
            AddToDom(_doc.StylesDom);
    }
}

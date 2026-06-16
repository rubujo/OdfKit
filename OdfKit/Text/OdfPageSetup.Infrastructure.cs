using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class OdfPageSetup
{
    #region Page Layout Infrastructure

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

    #endregion
}

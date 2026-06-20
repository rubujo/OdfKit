using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 代表 ODF 主控頁面（Master Page）。
/// </summary>
public sealed class OdfMasterPage
{
    private readonly OdfNode _node;

    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node => _node;

    /// <summary>
    /// 初始化 <see cref="OdfMasterPage"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體。</param>
    public OdfMasterPage(OdfNode node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    /// <summary>
    /// 取得或設定主控頁面的名稱。
    /// </summary>
    public string Name
    {
        get => _node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
        set => _node.SetAttribute("name", OdfNamespaces.Style, value, "style");
    }

    /// <summary>
    /// 取得或設定關聯的頁面版面名稱。
    /// </summary>
    public string? PageLayoutName
    {
        get => _node.GetAttribute("page-layout-name", OdfNamespaces.Style);
        set => _node.SetAttribute("page-layout-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定頁首的文字內容。
    /// </summary>
    public string? HeaderText
    {
        get => GetChildText("header");
        set => SetChildText("header", value);
    }

    /// <summary>
    /// 取得或設定頁尾的文字內容。
    /// </summary>
    public string? FooterText
    {
        get => GetChildText("footer");
        set => SetChildText("footer", value);
    }

    private string? GetChildText(string localName)
    {
        foreach (var child in _node.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.LocalName == "p" && grandChild.NamespaceUri == OdfNamespaces.Text)
                    {
                        return grandChild.TextContent;
                    }
                }
                return child.TextContent;
            }
        }
        return null;
    }

    private void SetChildText(string localName, string? value)
    {
        OdfNode? target = null;
        foreach (var child in _node.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Style)
            {
                target = child;
                break;
            }
        }

        if (value == null)
        {
            if (target != null)
                _node.RemoveChild(target);
        }
        else
        {
            if (target == null)
            {
                target = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Style, "style");
                _node.AppendChild(target);
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

            if (pNode == null)
            {
                pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                target.AppendChild(pNode);
            }
            pNode.TextContent = value;
        }
    }

    /// <summary>
    /// 取得或設定母片背景顏色（例如 <c>#ffffff</c>）。
    /// </summary>
    public string? BackgroundColor
    {
        get
        {
            OdfNode? properties = FindDrawingPageProperties();
            return properties?.GetAttribute("fill-color", OdfNamespaces.Draw);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                OdfNode? properties = FindDrawingPageProperties();
                if (properties is not null)
                    _node.RemoveChild(properties);
                return;
            }

            OdfNode pageProperties = FindOrCreateDrawingPageProperties();
            pageProperties.SetAttribute("fill", OdfNamespaces.Draw, "solid", "draw");
            pageProperties.SetAttribute("fill-color", OdfNamespaces.Draw, value!, "draw");
        }
    }

    private OdfNode? FindDrawingPageProperties()
    {
        foreach (OdfNode child in _node.Children)
        {
            if (child.LocalName == "drawing-page-properties" && child.NamespaceUri == OdfNamespaces.Style)
                return child;
        }
        return null;
    }

    private OdfNode FindOrCreateDrawingPageProperties()
    {
        OdfNode? properties = FindDrawingPageProperties();
        if (properties is not null)
            return properties;

        properties = new OdfNode(OdfNodeType.Element, "drawing-page-properties", OdfNamespaces.Style, "style");
        _node.AppendChild(properties);
        return properties;
    }
}

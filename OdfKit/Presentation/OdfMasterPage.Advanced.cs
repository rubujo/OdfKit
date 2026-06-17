using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public sealed partial class OdfMasterPage
{
    /// <summary>
    /// 取得或設定母片對應的頁面版面配置名稱。
    /// </summary>
    public string PageLayoutName
    {
        get => Node.GetAttribute("page-layout-name", OdfNamespaces.Style) ?? string.Empty;
        set => Node.SetAttribute("page-layout-name", OdfNamespaces.Style, value, "style");
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
                    Node.RemoveChild(properties);
                return;
            }

            OdfNode pageProperties = FindOrCreateDrawingPageProperties();
            pageProperties.SetAttribute("fill", OdfNamespaces.Draw, "solid", "draw");
            pageProperties.SetAttribute("fill-color", OdfNamespaces.Draw, value!, "draw");
        }
    }

    private OdfNode? FindDrawingPageProperties()
    {
        foreach (OdfNode child in Node.Children)
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
        Node.AppendChild(properties);
        return properties;
    }
}

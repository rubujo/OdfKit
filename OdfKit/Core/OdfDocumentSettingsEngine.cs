using System;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件設定（settings.xml）引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentSettingsEngine
{
    /// <summary>
    /// 尋找指定名稱的設定項目。
    /// </summary>
    internal static OdfNode? FindSettingsConfigItem(OdfNode settingsDom, string name)
    {
        return FindNodeByNameRecursive(settingsDom, "config-item", name);
    }

    /// <summary>
    /// 尋找或建立指定名稱的設定集合節點。
    /// </summary>
    internal static OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
    {
        foreach (var child in root.Children)
        {
            if (child.LocalName == "settings" && child.NamespaceUri == OdfNamespaces.Office)
            {
                foreach (var sc in child.Children)
                {
                    if (sc.LocalName == "config-item-set" && sc.GetAttribute("name", OdfNamespaces.Config) == name)
                        return sc;
                }
                var node = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
                node.SetAttribute("name", OdfNamespaces.Config, name, "config");
                child.AppendChild(node);
                return node;
            }
        }
        var sets = new OdfNode(OdfNodeType.Element, "settings", OdfNamespaces.Office, "office");
        var setNode = new OdfNode(OdfNodeType.Element, "config-item-set", OdfNamespaces.Config, "config");
        setNode.SetAttribute("name", OdfNamespaces.Config, name, "config");
        sets.AppendChild(setNode);
        root.AppendChild(sets);
        return setNode;
    }

    /// <summary>
    /// 尋找指定名稱的設定集合節點。
    /// </summary>
    internal static OdfNode? FindSettingsNode(OdfNode root, string name)
    {
        foreach (var child in root.Children)
        {
            if (child.LocalName == "settings" && child.NamespaceUri == OdfNamespaces.Office)
            {
                foreach (var sc in child.Children)
                {
                    if (sc.LocalName == "config-item-set" && sc.GetAttribute("name", OdfNamespaces.Config) == name)
                        return sc;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 尋找或建立設定 map 節點。
    /// </summary>
    internal static OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
    {
        foreach (var child in setNode.Children)
        {
            if (child.LocalName == "config-item-map-indexed" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, "config-item-map-indexed", OdfNamespaces.Config, "config");
        node.SetAttribute("name", OdfNamespaces.Config, name, "config");
        setNode.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 尋找或建立設定 map entry 節點。
    /// </summary>
    internal static OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
    {
        if (mapNode.Children.Count > 0)
            return mapNode.Children[0];
        var node = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
        mapNode.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 尋找或建立設定項目節點。
    /// </summary>
    internal static OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
    {
        foreach (var child in entryNode.Children)
        {
            if (child.LocalName == "config-item" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
        node.SetAttribute("name", OdfNamespaces.Config, name, "config");
        node.SetAttribute("type", OdfNamespaces.Config, type, "config");
        entryNode.AppendChild(node);
        return node;
    }

    /// <summary>
    /// 取得文件檢視縮放百分比。
    /// </summary>
    internal static double GetZoomLevel(OdfNode settingsDom)
    {
        var entry = FindSettingsConfigItem(settingsDom, "ZoomValue");
        if (entry != null && double.TryParse(entry.TextContent, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val;
        return 100.0;
    }

    /// <summary>
    /// 設定文件檢視縮放百分比。
    /// </summary>
    internal static void SetZoomLevel(OdfNode settingsDom, double zoom)
    {
        var setNode = FindOrCreateSettingsNode(settingsDom, "view-settings");
        var mapNode = FindOrCreateMapNode(setNode, "Views");
        var entryNode = FindOrCreateMapEntryNode(mapNode);
        var zoomNode = FindOrCreateConfigItemNode(entryNode, "ZoomValue", "int");
        zoomNode.TextContent = Math.Round(zoom).ToString();

        var zoomTypeNode = FindOrCreateConfigItemNode(entryNode, "ZoomType", "short");
        zoomTypeNode.TextContent = "0";
    }

    private static OdfNode? FindNodeByNameRecursive(OdfNode parent, string localName, string nameAttr)
    {
        if (parent.LocalName == localName && parent.GetAttribute("name", OdfNamespaces.Config) == nameAttr)
            return parent;
        foreach (var child in parent.Children)
        {
            var f = FindNodeByNameRecursive(child, localName, nameAttr);
            if (f != null)
                return f;
        }
        return null;
    }
}

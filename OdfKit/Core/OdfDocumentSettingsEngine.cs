using System;
using System.Globalization;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件設定（settings.xml）引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentSettingsEngine
{
    /// <summary>
    /// 尋找指定名稱的設定專案。
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
    /// 尋找或建立設定專案節點。
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
        if (entry != null && double.TryParse(entry.TextContent, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;
        return 100.0;
    }

    /// <summary>
    /// 設定文件檢視縮放百分比。
    /// </summary>
    internal static void SetZoomLevel(OdfNode settingsDom, double zoom)
    {
        var setNode = FindOrCreateSettingsNode(settingsDom, "ooo:view-settings");
        var mapNode = FindOrCreateMapNode(setNode, "Views");
        var entryNode = FindOrCreateMapEntryNode(mapNode);
        var zoomNode = FindOrCreateConfigItemNode(entryNode, "ZoomValue", "int");
        zoomNode.TextContent = Math.Round(zoom).ToString(CultureInfo.InvariantCulture);

        var zoomTypeNode = FindOrCreateConfigItemNode(entryNode, "ZoomType", "short");
        zoomTypeNode.TextContent = "0";
    }

    /// <summary>
    /// 設定開啟時是否更新欄位。
    /// </summary>
    internal static void SetUpdateFieldsWhenOpening(OdfNode settingsDom, bool update)
    {
        var setNode = FindOrCreateSettingsNode(settingsDom, "ooo:configuration-settings");
        var item = FindOrCreateConfigItemNode(setNode, "UpdateFieldsWhenOpening", "boolean");
        item.TextContent = update ? "true" : "false";
    }

    /// <summary>
    /// 設定連結更新模式。
    /// </summary>
    internal static void SetLinkUpdateMode(OdfNode settingsDom, int mode, bool isSpreadsheet)
    {
        string setName = isSpreadsheet ? "ooo:document-settings" : "ooo:configuration-settings";
        var setNode = FindOrCreateSettingsNode(settingsDom, setName);
        var item = FindOrCreateConfigItemNode(setNode, "LinkUpdateMode", "short");
        item.TextContent = mode.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 設定是否自動計算公式。
    /// </summary>
    internal static void SetAutoCalculate(OdfNode settingsDom, bool auto)
    {
        var setNode = FindOrCreateSettingsNode(settingsDom, "ooo:document-settings");
        var item = FindOrCreateConfigItemNode(setNode, "AutoCalculate", "boolean");
        item.TextContent = auto ? "true" : "false";
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

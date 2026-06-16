using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Helper Methods


    /// <summary>
    /// 尋找或建立 office:meta 根節點。
    /// </summary>
    /// <returns>office:meta 節點。</returns>
    protected OdfNode FindOrCreateMetaRoot()
    {
        foreach (var child in MetaDom.Children)
        {
            if (child.LocalName == "meta" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }
        var root = new OdfNode(OdfNodeType.Element, "meta", OdfNamespaces.Office, "office");
        MetaDom.AppendChild(root);
        return root;
    }

    private OdfNode? FindCustomPropertyNode(OdfNode metaRoot, string name)
    {
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "user-defined" &&
                child.NamespaceUri == OdfNamespaces.Meta &&
                child.GetAttribute("name", OdfNamespaces.Meta) == name)
            {
                return child;
            }
        }
        return null;
    }

    private string? GetMetaElementText(string qualifiedName)
    {
        var metaRoot = FindOrCreateMetaRoot();
        string localName = qualifiedName.Split(':')[1];
        string ns = qualifiedName.StartsWith("dc:") ? OdfNamespaces.Dc : OdfNamespaces.Meta;

        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child.TextContent;
        }
        return null;
    }

    private void SetMetaElementText(string qualifiedName, string? value)
    {
        var metaRoot = FindOrCreateMetaRoot();
        string[] parts = qualifiedName.Split(':');
        string localName = parts[1];
        string ns = parts[0] == "dc" ? OdfNamespaces.Dc : OdfNamespaces.Meta;
        string prefix = parts[0];

        OdfNode? target = null;
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
            {
                target = child;
                break;
            }
        }

        if (value == null)
        {
            if (target != null)
                metaRoot.RemoveChild(target);
        }
        else
        {
            if (target == null)
            {
                target = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
                metaRoot.AppendChild(target);
            }
            target.TextContent = value;
        }
    }

    private DateTime? ParseMetaDate(string? text)
    {
        if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var val))
        {
            if (val == DateTime.MinValue || val == DateTime.MaxValue)
                return val;
            try
            {
                return val.ToUniversalTime();
            }
            catch (ArgumentOutOfRangeException)
            {
                return val;
            }
        }
        return null;
    }

    private string? FormatMetaDate(DateTime? dt)
    {
        if (dt == null)
            return null;
        var val = dt.Value;
        if (val == DateTime.MinValue || val == DateTime.MaxValue)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        try
        {
            return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private string FormatValue(object val, string type)
    {
        return type.ToLowerInvariant() switch
        {
            "boolean" => ((bool)val) ? "true" : "false",
            "float" => Convert.ToDouble(val).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "date" => FormatDateValue((DateTime)val),
            _ => val.ToString() ?? string.Empty
        };
    }

    private string FormatDateValue(DateTime val)
    {
        if (val == DateTime.MinValue || val == DateTime.MaxValue)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        try
        {
            return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private object ParseValue(string val, string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "boolean" => bool.TryParse(val, out var b) && b,
            "float" => double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0.0,
            "date" => DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue,
            _ => val
        };
    }

    private double GetZoomLevelInternal()
    {
        var entry = FindSettingsConfigItem("ZoomValue");
        if (entry != null && double.TryParse(entry.TextContent, out var val))
            return val;
        return 100.0;
    }

    private void SetZoomLevelInternal(double zoom)
    {
        var settingsRoot = SettingsDom;
        var setNode = FindOrCreateSettingsNode(settingsRoot, "view-settings");
        var mapNode = FindOrCreateMapNode(setNode, "Views");
        var entryNode = FindOrCreateMapEntryNode(mapNode);
        var zoomNode = FindOrCreateConfigItemNode(entryNode, "ZoomValue", "int");
        zoomNode.TextContent = Math.Round(zoom).ToString();

        var zoomTypeNode = FindOrCreateConfigItemNode(entryNode, "ZoomType", "short");
        zoomTypeNode.TextContent = "0"; // 0: Direct Zoom percentage
    }

    /// <summary>
    /// 尋找指定名稱的設定項目。
    /// </summary>
    /// <param name="name">設定項目名稱。</param>
    /// <returns>設定項目節點；若不存在則為 <see langword="null"/>。</returns>
    protected OdfNode? FindSettingsConfigItem(string name)
    {
        return FindNodeByNameRecursive(SettingsDom, "config-item", name);
    }

    private OdfNode? FindNodeByNameRecursive(OdfNode parent, string localName, string nameAttr)
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

    /// <summary>
    /// 尋找或建立指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點。</param>
    /// <param name="name">設定集合名稱。</param>
    /// <returns>設定集合節點。</returns>
    protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
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
    /// <param name="root">設定 DOM 根節點。</param>
    /// <param name="name">設定集合名稱。</param>
    /// <returns>設定集合節點；若不存在則為 <see langword="null"/>。</returns>
    protected OdfNode? FindSettingsNode(OdfNode root, string name)
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
    /// <param name="setNode">設定集合節點。</param>
    /// <param name="name">map 名稱。</param>
    /// <returns>設定 map 節點。</returns>
    protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
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
    /// <param name="mapNode">設定 map 節點。</param>
    /// <returns>設定 map entry 節點。</returns>
    protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
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
    /// <param name="entryNode">設定 map entry 節點。</param>
    /// <param name="name">設定項目名稱。</param>
    /// <param name="type">設定項目類型。</param>
    /// <returns>設定項目節點。</returns>
    protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
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


    #endregion
}

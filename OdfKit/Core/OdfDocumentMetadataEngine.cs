using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件中記資料（meta.xml）引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentMetadataEngine
{
    /// <summary>
    /// 尋找或建立 office:meta 根節點。
    /// </summary>
    internal static OdfNode FindOrCreateMetaRoot(OdfNode metaDom)
    {
        foreach (var child in metaDom.Children)
        {
            if (child.LocalName == "meta" && child.NamespaceUri == OdfNamespaces.Office)
                return child;
        }
        var root = new OdfNode(OdfNodeType.Element, "meta", OdfNamespaces.Office, "office");
        metaDom.AppendChild(root);
        return root;
    }

    /// <summary>
    /// 取得指定 qualified name 的中繼資料元素文字內容。
    /// </summary>
    internal static string? GetMetaElementText(OdfNode metaDom, string qualifiedName)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
        string localName = qualifiedName.Split(':')[1];
        string ns = qualifiedName.StartsWith("dc:") ? OdfNamespaces.Dc : OdfNamespaces.Meta;

        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child.TextContent;
        }
        return null;
    }

    /// <summary>
    /// 設定指定 qualified name 的中繼資料元素文字內容。
    /// </summary>
    internal static void SetMetaElementText(OdfNode metaDom, string qualifiedName, string? value)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
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

    /// <summary>
    /// 解析中繼資料日期字串。
    /// </summary>
    internal static DateTime? ParseMetaDate(string? text)
    {
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var val))
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

    /// <summary>
    /// 格式化中繼資料日期值。
    /// </summary>
    internal static string? FormatMetaDate(DateTime? dt)
    {
        if (dt == null)
            return null;
        return FormatDateValue(dt.Value);
    }

    /// <summary>
    /// 設定自訂中繼資料屬性。
    /// </summary>
    internal static void SetCustomProperty(OdfNode metaDom, string name, object value, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be empty.", nameof(name));

        if (name.Contains(":"))
        {
            string oldName = name;
            name = name.Replace(":", "_");
            OdfKitDiagnostics.Warn($"Custom property name '{oldName}' contains invalid character ':'. Renamed to '{name}' for Excel compatibility.");
        }

        var metaRoot = FindOrCreateMetaRoot(metaDom);

        OdfNode? existing = FindCustomPropertyNode(metaRoot, name);
        if (existing != null)
            metaRoot.RemoveChild(existing);

        var propNode = new OdfNode(OdfNodeType.Element, "user-defined", OdfNamespaces.Meta, "meta");
        propNode.SetAttribute("name", OdfNamespaces.Meta, name, "meta");
        propNode.SetAttribute("value-type", OdfNamespaces.Meta, type, "meta");
        propNode.TextContent = FormatValue(value, type);

        metaRoot.AppendChild(propNode);
    }

    /// <summary>
    /// 取得自訂中繼資料屬性。
    /// </summary>
    internal static object? GetCustomProperty(OdfNode metaDom, string name)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
        var propNode = FindCustomPropertyNode(metaRoot, name);
        if (propNode == null)
            return null;

        string? type = propNode.GetAttribute("value-type", OdfNamespaces.Meta);
        string valStr = propNode.TextContent;
        return ParseValue(valStr, type);
    }

    /// <summary>
    /// 取得所有自訂中繼資料屬性的字典。
    /// </summary>
    internal static IReadOnlyDictionary<string, object?> GetAllCustomProperties(OdfNode metaDom)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
        var result = new Dictionary<string, object?>();
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "user-defined" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                string? n = child.GetAttribute("name", OdfNamespaces.Meta);
                if (!string.IsNullOrEmpty(n))
                {
                    string? type = child.GetAttribute("value-type", OdfNamespaces.Meta);
                    result[n!] = ParseValue(child.TextContent, type);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 更新文件統計中繼資料。
    /// </summary>
    internal static void UpdateDocumentStatistics(OdfNode metaDom, OdfNode contentDom)
    {
        int wordCount = 0;
        int charCount = 0;
        int paragraphCount = 0;
        int tableCount = 0;
        int imageCount = 0;

        TraverseForStats(contentDom, ref wordCount, ref charCount, ref paragraphCount, ref tableCount, ref imageCount);

        var metaRoot = FindOrCreateMetaRoot(metaDom);
        OdfNode? statNode = null;
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "document-statistic" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                statNode = child;
                break;
            }
        }

        if (statNode == null)
        {
            statNode = new OdfNode(OdfNodeType.Element, "document-statistic", OdfNamespaces.Meta, "meta");
            metaRoot.AppendChild(statNode);
        }

        statNode.SetAttribute("word-count", OdfNamespaces.Meta, wordCount.ToString(CultureInfo.InvariantCulture), "meta");
        statNode.SetAttribute("character-count", OdfNamespaces.Meta, charCount.ToString(CultureInfo.InvariantCulture), "meta");
        statNode.SetAttribute("paragraph-count", OdfNamespaces.Meta, paragraphCount.ToString(CultureInfo.InvariantCulture), "meta");
        statNode.SetAttribute("table-count", OdfNamespaces.Meta, tableCount.ToString(CultureInfo.InvariantCulture), "meta");
        statNode.SetAttribute("image-count", OdfNamespaces.Meta, imageCount.ToString(CultureInfo.InvariantCulture), "meta");
        statNode.SetAttribute("page-count", OdfNamespaces.Meta, "1", "meta");
    }

    /// <summary>
    /// 取得文件的範本中繼資料。
    /// </summary>
    internal static OdfTemplateMetadata? GetTemplateMetadata(OdfNode metaDom)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "template" && child.NamespaceUri == OdfNamespaces.Meta)
            {
                var meta = new OdfTemplateMetadata();
                meta.Href = child.GetAttribute("href", OdfNamespaces.XLink);
                meta.Title = child.GetAttribute("title", OdfNamespaces.XLink);
                meta.Date = ParseMetaDate(child.GetAttribute("date", OdfNamespaces.Meta));
                return meta;
            }
        }
        return null;
    }

    /// <summary>
    /// 設定文件的範本中繼資料。
    /// </summary>
    internal static void SetTemplateMetadata(OdfNode metaDom, OdfTemplateMetadata? value)
    {
        var metaRoot = FindOrCreateMetaRoot(metaDom);
        OdfNode? target = null;
        foreach (var child in metaRoot.Children)
        {
            if (child.LocalName == "template" && child.NamespaceUri == OdfNamespaces.Meta)
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
                target = new OdfNode(OdfNodeType.Element, "template", OdfNamespaces.Meta, "meta");
                metaRoot.AppendChild(target);
            }
            target.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");

            if (value.Href != null)
                target.SetAttribute("href", OdfNamespaces.XLink, value.Href, "xlink");
            else
                target.RemoveAttribute("href", OdfNamespaces.XLink);

            if (value.Title != null)
                target.SetAttribute("title", OdfNamespaces.XLink, value.Title, "xlink");
            else
                target.RemoveAttribute("title", OdfNamespaces.XLink);

            if (value.Date != null)
                target.SetAttribute("date", OdfNamespaces.Meta, FormatMetaDate(value.Date) ?? "", "meta");
            else
                target.RemoveAttribute("date", OdfNamespaces.Meta);
        }
    }

    private static OdfNode? FindCustomPropertyNode(OdfNode metaRoot, string name)
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

    private static string FormatValue(object val, string type)
    {
        return type.ToLowerInvariant() switch
        {
            "boolean" => ((bool)val) ? "true" : "false",
            "float" => Convert.ToDouble(val).ToString(CultureInfo.InvariantCulture),
            "date" => FormatDateValue((DateTime)val),
            _ => val.ToString() ?? string.Empty
        };
    }

    private static string FormatDateValue(DateTime val)
    {
        if (val == DateTime.MinValue || val == DateTime.MaxValue)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        try
        {
            return val.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return val.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }

    private static object ParseValue(string val, string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "boolean" => bool.TryParse(val, out var b) && b,
            "float" => double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0.0,
            "date" => DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.MinValue,
            _ => val
        };
    }

    private static void TraverseForStats(OdfNode node, ref int words, ref int chars, ref int paragraphs, ref int tables, ref int images)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            chars += text.Length;

            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            words += parts.Length;
            return;
        }

        if (node.LocalName == "p" && node.NamespaceUri == OdfNamespaces.Text)
            paragraphs++;
        else if (node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
            tables++;
        else if (node.LocalName == "image" && node.NamespaceUri == OdfNamespaces.Draw)
            images++;

        foreach (var child in node.Children)
        {
            TraverseForStats(child, ref words, ref chars, ref paragraphs, ref tables, ref images);
        }
    }
}

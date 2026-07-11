using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 將試算表外部連結快取儲存至 <c>settings.xml</c> 的內部協作者。
/// </summary>
internal static class OdfExternalLinkPersistenceEngine
{
    private const string DocumentSettingsName = "ooo:document-settings";
    private const string ExternalLinksMapName = "OdfKitExternalLinks";

    internal static OdfExternalLinkManager Load(OdfNode settingsDom)
    {
        var manager = new OdfExternalLinkManager();
        OdfNode? docSettings = OdfDocumentSettingsEngine.FindSettingsNode(settingsDom, DocumentSettingsName);
        OdfNode? map = docSettings is null ? null : FindConfigChild(docSettings, "config-item-map-indexed", ExternalLinksMapName);
        if (map is null)
        {
            return manager;
        }

        foreach (OdfNode entry in map.Children)
        {
            if (entry.LocalName != "config-item-map-entry" || entry.NamespaceUri != OdfNamespaces.Config)
            {
                continue;
            }

            string? documentId = FindConfigItemValue(entry, "DocumentId");
            string? sheetName = FindConfigItemValue(entry, "SheetName");
            string? rowText = FindConfigItemValue(entry, "Row");
            string? columnText = FindConfigItemValue(entry, "Column");
            string? valueType = FindConfigItemValue(entry, "ValueType");
            string? valueText = FindConfigItemValue(entry, "Value");
            if (string.IsNullOrEmpty(documentId) ||
                string.IsNullOrEmpty(sheetName) ||
                !int.TryParse(rowText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ||
                !int.TryParse(columnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int column))
            {
                continue;
            }

            object? value = ParseValue(valueType, valueText);
            manager.SetCachedValue(documentId!, sheetName!, new OdfCellAddress(row, column), value);
        }

        return manager;
    }

    internal static void Save(OdfNode settingsDom, OdfExternalLinkManager manager)
    {
        var cachedCells = new List<OdfExternalLinkManager.CachedCell>(manager.GetCachedCells());
        OdfNode docSettings = OdfDocumentSettingsEngine.FindOrCreateSettingsNode(settingsDom, DocumentSettingsName);
        OdfNode? map = FindConfigChild(docSettings, "config-item-map-indexed", ExternalLinksMapName);
        if (map is not null)
        {
            foreach (OdfNode child in new List<OdfNode>(map.Children))
            {
                if (IsManagedEntry(child))
                    map.RemoveChild(child);
            }
        }

        if (cachedCells.Count == 0)
        {
            if (map is not null && map.Children.Count == 0)
                docSettings.RemoveChild(map);
            return;
        }

        if (map is null)
        {
            map = new OdfNode(OdfNodeType.Element, "config-item-map-indexed", OdfNamespaces.Config, "config");
            map.SetAttribute("name", OdfNamespaces.Config, ExternalLinksMapName, "config");
            docSettings.AppendChild(map);
        }

        foreach (OdfExternalLinkManager.CachedCell cell in cachedCells)
        {
            var entry = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
            map.AppendChild(entry);

            WriteConfigItem(entry, "DocumentId", "string", cell.DocumentId);
            WriteConfigItem(entry, "SheetName", "string", cell.SheetName);
            WriteConfigItem(entry, "Row", "int", cell.Row.ToString(CultureInfo.InvariantCulture));
            WriteConfigItem(entry, "Column", "int", cell.Column.ToString(CultureInfo.InvariantCulture));
            SerializeValue(cell.Value, out string valueType, out string valueText);
            WriteConfigItem(entry, "ValueType", "string", valueType);
            WriteConfigItem(entry, "Value", "string", valueText);
        }
    }

    private static bool IsManagedEntry(OdfNode entry) =>
        entry.LocalName == "config-item-map-entry" &&
        entry.NamespaceUri == OdfNamespaces.Config &&
        FindConfigItemValue(entry, "DocumentId") is not null &&
        FindConfigItemValue(entry, "SheetName") is not null &&
        FindConfigItemValue(entry, "Row") is not null &&
        FindConfigItemValue(entry, "Column") is not null;

    private static OdfNode? FindConfigChild(OdfNode parent, string localName, string name)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                return child;
            }
        }

        return null;
    }

    private static string? FindConfigItemValue(OdfNode entry, string name)
    {
        foreach (OdfNode child in entry.Children)
        {
            if (child.LocalName == "config-item" &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                return child.TextContent;
            }
        }

        return null;
    }

    private static void WriteConfigItem(OdfNode entry, string name, string type, string value)
    {
        var item = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
        item.SetAttribute("name", OdfNamespaces.Config, name, "config");
        item.SetAttribute("type", OdfNamespaces.Config, type, "config");
        item.TextContent = value;
        entry.AppendChild(item);
    }

    private static void SerializeValue(object? value, out string valueType, out string valueText)
    {
        switch (value)
        {
            case null:
                valueType = "empty";
                valueText = string.Empty;
                return;
            case bool flag:
                valueType = "boolean";
                valueText = flag ? "true" : "false";
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                valueType = "float";
                valueText = Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                return;
            default:
                valueType = "string";
                valueText = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return;
        }
    }

    private static object? ParseValue(string? valueType, string? valueText)
    {
        return valueType switch
        {
            "empty" => null,
            "boolean" => string.Equals(valueText, "true", StringComparison.OrdinalIgnoreCase),
            "float" => double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : 0.0,
            _ => valueText ?? string.Empty
        };
    }
}

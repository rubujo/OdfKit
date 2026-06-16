using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region View

    /// <summary>
    /// 凍結指定數量的上方列與左側欄。
    /// </summary>
    /// <param name="frozenRows">要凍結的列數。</param>
    /// <param name="frozenColumns">要凍結的欄數。</param>
    /// <exception cref="ArgumentOutOfRangeException">當列數或欄數小於 0 時擲出。</exception>
    public void FreezePanes(int frozenRows, int frozenColumns)
    {
        if (frozenRows < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenRows));
        if (frozenColumns < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenColumns));

        TableNode.SetAttribute("frozen-rows", OdfNamespaces.Table, frozenRows.ToString(CultureInfo.InvariantCulture), "table");
        TableNode.SetAttribute("frozen-columns", OdfNamespaces.Table, frozenColumns.ToString(CultureInfo.InvariantCulture), "table");

        var viewSettings = _doc.GetOrCreateSettingsItemSet("view-settings");
        var views = FindOrCreateChild(viewSettings, "config-item-map-indexed", OdfNamespaces.Config, "config");
        views.SetAttribute("name", OdfNamespaces.Config, "Views", "config");
        var viewEntry = FindOrCreateFirstChild(views, "config-item-map-entry", OdfNamespaces.Config, "config");
        var tables = FindOrCreateChild(viewEntry, "config-item-map-named", OdfNamespaces.Config, "config");
        tables.SetAttribute("name", OdfNamespaces.Config, "Tables", "config");
        var sheetEntry = FindOrCreateNamedMapEntry(tables, Name);

        SetConfigItem(sheetEntry, "HorizontalSplitMode", "short", frozenRows > 0 ? "2" : "0");
        SetConfigItem(sheetEntry, "VerticalSplitMode", "short", frozenColumns > 0 ? "2" : "0");
        SetConfigItem(sheetEntry, "HorizontalSplitPosition", "int", frozenRows.ToString(CultureInfo.InvariantCulture));
        SetConfigItem(sheetEntry, "VerticalSplitPosition", "int", frozenColumns.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 以分割模式（非凍結）分割工作表窗格。
    /// </summary>
    /// <param name="splitRow">水平分割線所在的列索引 (0 表示不分割)。</param>
    /// <param name="splitColumn">垂直分割線所在的欄索引 (0 表示不分割)。</param>
    /// <exception cref="ArgumentOutOfRangeException">當列索引或欄索引小於 0 時拋出。</exception>
    public void SplitPanes(int splitRow, int splitColumn)
    {
        if (splitRow < 0)
            throw new ArgumentOutOfRangeException(nameof(splitRow));
        if (splitColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(splitColumn));

        var viewSettings = _doc.GetOrCreateSettingsItemSet("view-settings");
        var views = FindOrCreateChild(viewSettings, "config-item-map-indexed", OdfNamespaces.Config, "config");
        views.SetAttribute("name", OdfNamespaces.Config, "Views", "config");
        var viewEntry = FindOrCreateFirstChild(views, "config-item-map-entry", OdfNamespaces.Config, "config");
        var tables = FindOrCreateChild(viewEntry, "config-item-map-named", OdfNamespaces.Config, "config");
        tables.SetAttribute("name", OdfNamespaces.Config, "Tables", "config");
        var sheetEntry = FindOrCreateNamedMapEntry(tables, Name);

        SetConfigItem(sheetEntry, "HorizontalSplitMode", "short", splitRow > 0 ? "1" : "0");
        SetConfigItem(sheetEntry, "VerticalSplitMode", "short", splitColumn > 0 ? "1" : "0");
        SetConfigItem(sheetEntry, "HorizontalSplitPosition", "int", splitRow.ToString(CultureInfo.InvariantCulture));
        SetConfigItem(sheetEntry, "VerticalSplitPosition", "int", splitColumn.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 取得目前工作表的凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes
    {
        get
        {
            int frozenRows = ParseNonNegativeInt(TableNode.GetAttribute("frozen-rows", OdfNamespaces.Table));
            int frozenColumns = ParseNonNegativeInt(TableNode.GetAttribute("frozen-columns", OdfNamespaces.Table));
            return new OdfFrozenPanes(frozenRows, frozenColumns);
        }
    }

    /// <summary>
    /// 新增清單型資料驗證，並套用到指定範圍。
    /// </summary>
    /// <param name="range">要套用的儲存格範圍。</param>
    /// <param name="name">驗證規則名稱。</param>
    /// <param name="allowedValues">允許的值。</param>
    public void AddValidationList(OdfCellRange range, string name, params string[] allowedValues)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("驗證名稱不可空白。", nameof(name));
        if (allowedValues is null || allowedValues.Length == 0)
            throw new ArgumentException("驗證清單至少需要一個允許值。", nameof(allowedValues));

        var validations = FindOrCreateChild(TableNode, "content-validations", OdfNamespaces.Table, "table");
        var validation = FindOrCreateNamedChild(validations, "content-validation", name);
        validation.SetAttribute("name", OdfNamespaces.Table, name, "table");
        validation.SetAttribute("condition", OdfNamespaces.Table, BuildValidationListCondition(allowedValues), "table");
        validation.SetAttribute("allow-empty-cell", OdfNamespaces.Table, "true", "table");

        foreach (OdfCell cell in EnumerateCells(range))
        {
            cell.Node.SetAttribute("content-validation-name", OdfNamespaces.Table, name, "table");
        }
    }

    private IEnumerable<OdfCell> EnumerateCells(OdfCellRange range)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startCol; col <= endCol; col++)
            {
                yield return GetCell(row, col);
            }
        }
    }

    private static string BuildValidationListCondition(IEnumerable<string> values)
    {
        var quoted = new List<string>();
        foreach (string value in values)
        {
            quoted.Add("\"" + value.Replace("\"", "\"\"") + "\"");
        }

        return "cell-content-is-in-list(" + string.Join(";", quoted) + ")";
    }

    private OdfNode FindOrCreateNamedChild(OdfNode parent, string localName, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Table &&
                child.GetAttribute("name", OdfNamespaces.Table) == name)
            {
                return child;
            }
        }

        var node = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Table, "table");
        parent.AppendChild(node);
        return node;
    }

    private static OdfNode FindOrCreateFirstChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
            {
                return child;
            }
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }

    private static OdfNode FindOrCreateNamedMapEntry(OdfNode parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == "config-item-map-entry" &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                return child;
            }
        }

        var entry = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
        entry.SetAttribute("name", OdfNamespaces.Config, name, "config");
        parent.AppendChild(entry);
        return entry;
    }

    private static void SetConfigItem(OdfNode entry, string name, string type, string value)
    {
        OdfNode? item = null;
        foreach (var child in entry.Children)
        {
            if (child.LocalName == "config-item" &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                item = child;
                break;
            }
        }

        if (item is null)
        {
            item = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
            item.SetAttribute("name", OdfNamespaces.Config, name, "config");
            entry.AppendChild(item);
        }

        item.SetAttribute("type", OdfNamespaces.Config, type, "config");
        item.TextContent = value;
    }


    #endregion
}

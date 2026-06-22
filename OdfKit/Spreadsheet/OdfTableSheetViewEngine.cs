using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表檢視與資料驗證引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetViewEngine
{
    internal static void FreezePanes(OdfTableSheetMutationContext context, int frozenRows, int frozenColumns)
    {
        if (frozenRows < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenRows));
        if (frozenColumns < 0)
            throw new ArgumentOutOfRangeException(nameof(frozenColumns));

        context.TableNode.SetAttribute("frozen-rows", OdfNamespaces.Table, frozenRows.ToString(CultureInfo.InvariantCulture), "table");
        context.TableNode.SetAttribute("frozen-columns", OdfNamespaces.Table, frozenColumns.ToString(CultureInfo.InvariantCulture), "table");

        ApplySplitConfig(context, frozenRows, frozenColumns, freezeMode: true);
    }

    internal static void SplitPanes(OdfTableSheetMutationContext context, int splitRow, int splitColumn)
    {
        if (splitRow < 0)
            throw new ArgumentOutOfRangeException(nameof(splitRow));
        if (splitColumn < 0)
            throw new ArgumentOutOfRangeException(nameof(splitColumn));

        ApplySplitConfig(context, splitRow, splitColumn, freezeMode: false);
    }

    internal static OdfFrozenPanes GetFrozenPanes(OdfTableSheetMutationContext context)
    {
        int frozenRows = OdfTableSheetDomHelper.ParseNonNegativeInt(
            context.TableNode.GetAttribute("frozen-rows", OdfNamespaces.Table));
        int frozenColumns = OdfTableSheetDomHelper.ParseNonNegativeInt(
            context.TableNode.GetAttribute("frozen-columns", OdfNamespaces.Table));
        return new OdfFrozenPanes(frozenRows, frozenColumns);
    }

    internal static OdfSplitPanes GetSplitPanes(OdfTableSheetMutationContext context)
    {
        OdfNode? sheetEntry = FindSheetViewConfigEntry(context.Document.SettingsDom, context.SheetName);
        if (sheetEntry is null)
            return new OdfSplitPanes(0, 0);

        int splitRows = ReadSplitAxis(sheetEntry, "HorizontalSplitMode", "HorizontalSplitPosition");
        int splitColumns = ReadSplitAxis(sheetEntry, "VerticalSplitMode", "VerticalSplitPosition");
        return new OdfSplitPanes(splitRows, splitColumns);
    }

    private static int ReadSplitAxis(OdfNode sheetEntry, string modeName, string positionName)
    {
        string? mode = ReadConfigItemValue(sheetEntry, modeName);
        if (mode != "1")
            return 0;

        return OdfTableSheetDomHelper.ParseNonNegativeInt(ReadConfigItemValue(sheetEntry, positionName));
    }

    private static OdfNode? FindSheetViewConfigEntry(OdfNode settingsDom, string sheetName)
    {
        OdfNode? viewSettings = OdfDocumentSettingsEngine.FindSettingsNode(settingsDom, "view-settings");
        if (viewSettings is null)
            return null;

        OdfNode? viewsMap = FindNamedConfigChild(viewSettings, "config-item-map-indexed", "Views");
        if (viewsMap is null)
            return null;

        OdfNode? viewEntry = FindFirstConfigChild(viewsMap, "config-item-map-entry");
        if (viewEntry is null)
            return null;

        OdfNode? tablesMap = FindNamedConfigChild(viewEntry, "config-item-map-named", "Tables");
        if (tablesMap is null)
            return null;

        return FindNamedConfigChild(tablesMap, "config-item-map-entry", sheetName);
    }

    private static OdfNode? FindNamedConfigChild(OdfNode parent, string localName, string name)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != localName ||
                child.NamespaceUri != OdfNamespaces.Config)
                continue;

            if (child.GetAttribute("name", OdfNamespaces.Config) == name)
                return child;
        }

        return null;
    }

    private static OdfNode? FindFirstConfigChild(OdfNode parent, string localName)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Config)
                return child;
        }

        return null;
    }

    private static string? ReadConfigItemValue(OdfNode entry, string name)
    {
        foreach (OdfNode child in entry.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "config-item" ||
                child.NamespaceUri != OdfNamespaces.Config)
                continue;

            if (child.GetAttribute("name", OdfNamespaces.Config) == name)
                return child.TextContent;
        }

        return null;
    }

    internal static void AddValidationList(
        OdfTableSheetMutationContext context,
        OdfCellRange range,
        string name,
        string[] allowedValues)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfTableSheetViewEngine_VerificationCannotBeEmpty"), nameof(name));
        if (allowedValues is null || allowedValues.Length == 0)
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfTableSheetViewEngine_ValidationManifestRequiresLeast"), nameof(allowedValues));

        // table:content-validations 須為 office:spreadsheet 的直接子節點（所有工作表共用的全域宣告），
        // 不可放在個別 table:table 內部，否則真實 LibreOffice 會視為結構不合法並靜默捨棄整條規則。
        var sheetsRoot = context.Document.SheetsRoot;
        OdfNode? validations = null;
        foreach (var child in sheetsRoot.Children)
        {
            if (child.LocalName == "content-validations" && child.NamespaceUri == OdfNamespaces.Table)
            {
                validations = child;
                break;
            }
        }
        if (validations is null)
        {
            validations = new OdfNode(OdfNodeType.Element, "content-validations", OdfNamespaces.Table, "table");
            if (sheetsRoot.Children.Count > 0)
                sheetsRoot.InsertBefore(validations, sheetsRoot.Children[0]);
            else
                sheetsRoot.AppendChild(validations);
        }
        var validation = FindOrCreateNamedChild(validations, "content-validation", name);
        validation.SetAttribute("name", OdfNamespaces.Table, name, "table");
        validation.SetAttribute("condition", OdfNamespaces.Table, BuildValidationListCondition(allowedValues), "table");
        validation.SetAttribute("allow-empty-cell", OdfNamespaces.Table, "true", "table");
        validation.SetAttribute("display-list", OdfNamespaces.Table, "unsorted", "table");
        validation.SetAttribute("base-cell-address", OdfNamespaces.Table, $"{context.SheetName}.A1", "table");

        foreach (OdfCell cell in EnumerateCells(context, range))
            cell.Node.SetAttribute("content-validation-name", OdfNamespaces.Table, name, "table");
    }

    private static void ApplySplitConfig(
        OdfTableSheetMutationContext context,
        int horizontal,
        int vertical,
        bool freezeMode)
    {
        var viewSettings = context.Document.GetOrCreateSettingsItemSet("view-settings");
        var views = OdfTableSheetDomHelper.FindOrCreateChild(viewSettings, "config-item-map-indexed", OdfNamespaces.Config, "config");
        views.SetAttribute("name", OdfNamespaces.Config, "Views", "config");
        var viewEntry = FindOrCreateFirstChild(views, "config-item-map-entry", OdfNamespaces.Config, "config");
        var tables = OdfTableSheetDomHelper.FindOrCreateChild(viewEntry, "config-item-map-named", OdfNamespaces.Config, "config");
        tables.SetAttribute("name", OdfNamespaces.Config, "Tables", "config");
        var sheetEntry = FindOrCreateNamedMapEntry(tables, context.SheetName);

        string hMode = horizontal > 0 ? (freezeMode ? "2" : "1") : "0";
        string vMode = vertical > 0 ? (freezeMode ? "2" : "1") : "0";
        SetConfigItem(sheetEntry, "HorizontalSplitMode", "short", hMode);
        SetConfigItem(sheetEntry, "VerticalSplitMode", "short", vMode);
        SetConfigItem(sheetEntry, "HorizontalSplitPosition", "int", horizontal.ToString(CultureInfo.InvariantCulture));
        SetConfigItem(sheetEntry, "VerticalSplitPosition", "int", vertical.ToString(CultureInfo.InvariantCulture));
    }

    private static IEnumerable<OdfCell> EnumerateCells(OdfTableSheetMutationContext context, OdfCellRange range)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startCol; col <= endCol; col++)
                yield return context.GetCell(row, col);
        }
    }

    private static string BuildValidationListCondition(IEnumerable<string> values)
    {
        var quoted = new List<string>();
        foreach (string value in values)
            quoted.Add("\"" + value.Replace("\"", "\"\"") + "\"");
        return "of:cell-content-is-in-list(" + string.Join(";", quoted) + ")";
    }

    private static OdfNode FindOrCreateNamedChild(OdfNode parent, string localName, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Table &&
                child.GetAttribute("name", OdfNamespaces.Table) == name)
                return child;
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
                return child;
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
                return child;
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
}

using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表樞紐分析表讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentPivotTableReadEngine
{
    internal static IReadOnlyList<OdfPivotTableInfo> GetPivotTables(SpreadsheetDocument document)
    {
        List<OdfPivotTableInfo> pivotTables = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            OdfNode? tablesContainer = OdfTableSheetDomHelper.FindChildElement(
                sheet.TableNode, "data-pilot-tables", OdfNamespaces.Table);
            if (tablesContainer is null)
                continue;

            foreach (OdfNode child in tablesContainer.Children)
            {
                if (child.NodeType is not OdfNodeType.Element ||
                    child.LocalName != "data-pilot-table" ||
                    child.NamespaceUri != OdfNamespaces.Table)
                    continue;

                string? name = child.GetAttribute("name", OdfNamespaces.Table);
                if (string.IsNullOrEmpty(name))
                    continue;

                pivotTables.Add(new OdfPivotTableInfo(
                    sheet.Name,
                    name!,
                    ParseSourceRangeAddress(child),
                    child.GetAttribute("target-range-address", OdfNamespaces.Table) ?? string.Empty,
                    child.GetAttribute("has-column-headers", OdfNamespaces.Table) != "false",
                    child.GetAttribute("has-row-headers", OdfNamespaces.Table) != "false",
                    ParseFields(child),
                    ParseSortFields(child),
                    ParseFilterConditions(child)));
            }
        }

        return pivotTables.AsReadOnly();
    }

    private static string ParseSourceRangeAddress(OdfNode pivotTableNode)
    {
        foreach (OdfNode child in pivotTableNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "source-cell-range" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            return child.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty;
        }

        return string.Empty;
    }

    private static IReadOnlyList<OdfPivotTableFieldInfo> ParseFields(OdfNode pivotTableNode)
    {
        List<OdfPivotTableFieldInfo> fields = [];

        foreach (OdfNode child in pivotTableNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "data-pilot-field" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            string? sourceFieldName = child.GetAttribute("source-field-name", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(sourceFieldName))
                continue;

            fields.Add(new OdfPivotTableFieldInfo(
                sourceFieldName!,
                child.GetAttribute("orientation", OdfNamespaces.Table) ?? string.Empty,
                child.GetAttribute("function", OdfNamespaces.Table),
                child.GetAttribute("formula", OdfNamespaces.Table)));
        }

        return fields.AsReadOnly();
    }

    private static IReadOnlyList<OdfPivotTableSortFieldInfo> ParseSortFields(OdfNode pivotTableNode)
    {
        List<OdfPivotTableSortFieldInfo> sortFields = [];

        foreach (OdfNode child in pivotTableNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "sort-info" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            foreach (OdfNode sortFieldNode in child.Children)
            {
                if (sortFieldNode.NodeType is not OdfNodeType.Element ||
                    sortFieldNode.LocalName != "sort-field" ||
                    sortFieldNode.NamespaceUri != OdfNamespaces.Table)
                    continue;

                string? sourceFieldName = sortFieldNode.GetAttribute("source-field-name", OdfNamespaces.Table);
                if (string.IsNullOrEmpty(sourceFieldName))
                    continue;

                bool ascending = sortFieldNode.GetAttribute("order", OdfNamespaces.Table) != "descending";
                sortFields.Add(new OdfPivotTableSortFieldInfo(sourceFieldName!, ascending));
            }
        }

        return sortFields.AsReadOnly();
    }

    private static IReadOnlyList<OdfPivotTableFilterConditionInfo> ParseFilterConditions(OdfNode pivotTableNode)
    {
        List<OdfPivotTableFilterConditionInfo> conditions = [];

        foreach (OdfNode child in pivotTableNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "filter" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            foreach (OdfNode conditionNode in child.Children)
            {
                if (conditionNode.NodeType is not OdfNodeType.Element ||
                    conditionNode.LocalName != "filter-condition" ||
                    conditionNode.NamespaceUri != OdfNamespaces.Table)
                    continue;

                string? sourceFieldName = conditionNode.GetAttribute("source-field-name", OdfNamespaces.Table);
                if (string.IsNullOrEmpty(sourceFieldName))
                    continue;

                conditions.Add(new OdfPivotTableFilterConditionInfo(
                    sourceFieldName!,
                    conditionNode.GetAttribute("operator", OdfNamespaces.Table) ?? string.Empty,
                    conditionNode.GetAttribute("value", OdfNamespaces.Table) ?? string.Empty));
            }
        }

        return conditions.AsReadOnly();
    }
}

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

        // 依 ODF 1.4 schema，table:data-pilot-tables 是 office:spreadsheet 的直接子節點
        // （與所有 table:table 同層），而非個別工作表內部的子節點。
        OdfNode? tablesContainer = OdfTableSheetDomHelper.FindChildElement(
            document.SheetsRoot, "data-pilot-tables", OdfNamespaces.Table);
        if (tablesContainer is null)
            return pivotTables.AsReadOnly();

        foreach (OdfNode child in tablesContainer.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "data-pilot-table" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            string? name = child.GetAttribute("name", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(name))
                continue;

            string targetRangeAddress = child.GetAttribute("target-range-address", OdfNamespaces.Table) ?? string.Empty;
            string sheetName = ResolveSheetNameFromTargetRange(targetRangeAddress);

            pivotTables.Add(new OdfPivotTableInfo(
                sheetName,
                name!,
                ParseSourceRangeAddress(child),
                targetRangeAddress,
                child.GetAttribute("has-column-headers", OdfNamespaces.Table) != "false",
                child.GetAttribute("has-row-headers", OdfNamespaces.Table) != "false",
                ParseFields(child),
                ParseSortFields(child),
                ParseFilterConditions(child))
            {
                GrandTotals = child.GetAttribute("grand-total", OdfNamespaces.Table) switch
                {
                    "row" => OdfPivotGrandTotal.Row,
                    "column" => OdfPivotGrandTotal.Column,
                    "both" => OdfPivotGrandTotal.Both,
                    _ => OdfPivotGrandTotal.None,
                },
                ShowFilterButton = child.GetAttribute("show-filter-button", OdfNamespaces.Table) != "false",
                DrillDownOnDoubleClick =
                    child.GetAttribute("drill-down-on-double-click", OdfNamespaces.Table) == "true",
            });
        }

        return pivotTables.AsReadOnly();
    }

    /// <summary>
    /// 從 <c>table:target-range-address</c> 字串解析所在工作表名稱。
    /// </summary>
    /// <remarks>
    /// 依 ODF 1.4 schema，該屬性型別為 <c>cellRangeAddress</c>（範圍），優先以範圍格式解析；
    /// 若為舊版本寫入之單一儲存格位址格式，則改採以單一位址格式解析，以維持向下相容。
    /// </remarks>
    private static string ResolveSheetNameFromTargetRange(string targetRangeAddress)
    {
        if (OdfCellRange.TryParse(targetRangeAddress, out OdfCellRange range))
            return range.StartAddress.SheetName ?? string.Empty;

        if (OdfCellAddress.TryParse(targetRangeAddress, out OdfCellAddress address))
            return address.SheetName ?? string.Empty;

        return string.Empty;
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

    private static System.Collections.ObjectModel.ReadOnlyCollection<OdfPivotTableFieldInfo> ParseFields(OdfNode pivotTableNode)
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

            var info = new OdfPivotTableFieldInfo(
                sourceFieldName!,
                child.GetAttribute("orientation", OdfNamespaces.Table) ?? string.Empty,
                child.GetAttribute("function", OdfNamespaces.Table),
                child.GetAttribute("formula", OdfNamespaces.Table));
            ParseAdvancedFieldInfo(child, info);
            fields.Add(info);
        }

        return fields.AsReadOnly();
    }

    private static void ParseAdvancedFieldInfo(OdfNode fieldNode, OdfPivotTableFieldInfo info)
    {
        foreach (OdfNode child in fieldNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Table)
                continue;
            if (child.LocalName == "data-pilot-level")
            {
                foreach (OdfNode levelChild in child.Children)
                {
                    if (levelChild.NamespaceUri == OdfNamespaces.Table &&
                        levelChild.LocalName == "data-pilot-layout-info")
                    {
                        info.Layout = levelChild.GetAttribute("layout-mode", OdfNamespaces.Table) switch
                        {
                            "outline-subtotals-bottom" => OdfPivotLayout.OutlineSubtotalsBottom,
                            "outline-subtotals-top" => OdfPivotLayout.OutlineSubtotalsTop,
                            _ => OdfPivotLayout.Tabular,
                        };
                    }
                }
            }
            else if (child.LocalName == "data-pilot-groups")
            {
                info.Grouping = ParseGrouping(child);
            }
            else if (child.LocalName == "data-pilot-field-reference")
            {
                info.ValueOptions = new OdfPivotValueOptions
                {
                    ShowValuesAs = child.GetAttribute("type", OdfNamespaces.Table) switch
                    {
                        "row-percentage" => OdfPivotShowValuesAs.PercentageOfRowTotal,
                        "column-percentage" => OdfPivotShowValuesAs.PercentageOfColumnTotal,
                        "total-percentage" => OdfPivotShowValuesAs.PercentageOfGrandTotal,
                        "running-total" => OdfPivotShowValuesAs.RunningTotal,
                        "member-difference" => OdfPivotShowValuesAs.DifferenceFrom,
                        "member-percentage-difference" => OdfPivotShowValuesAs.PercentageDifferenceFrom,
                        "index" => OdfPivotShowValuesAs.Index,
                        _ => OdfPivotShowValuesAs.None,
                    },
                    BaseFieldName = child.GetAttribute("field-name", OdfNamespaces.Table),
                    BaseMemberName = child.GetAttribute("member-name", OdfNamespaces.Table),
                };
            }
        }
    }

    private static OdfPivotGroupingOptions ParseGrouping(OdfNode node)
    {
        if (!string.IsNullOrEmpty(node.GetAttribute("start", OdfNamespaces.Table)))
        {
            return new OdfPivotGroupingOptions
            {
                Start = ParseDouble(node.GetAttribute("start", OdfNamespaces.Table)),
                End = ParseDouble(node.GetAttribute("end", OdfNamespaces.Table)),
                Interval = ParseDouble(node.GetAttribute("step", OdfNamespaces.Table)),
            };
        }
        string? groupedBy = node.GetAttribute("grouped-by", OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(groupedBy))
        {
            return new OdfPivotGroupingOptions
            {
                DateGroup = groupedBy switch
                {
                    "years" => OdfPivotDateGroup.Years,
                    "quarters" => OdfPivotDateGroup.Quarters,
                    "months" => OdfPivotDateGroup.Months,
                    "days" => OdfPivotDateGroup.Days,
                    "hours" => OdfPivotDateGroup.Hours,
                    "minutes" => OdfPivotDateGroup.Minutes,
                    "seconds" => OdfPivotDateGroup.Seconds,
                    _ => null,
                },
            };
        }
        return new OdfPivotGroupingOptions();
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double parsed) &&
        !double.IsNaN(parsed) &&
        !double.IsInfinity(parsed)
            ? parsed
            : null;

    private static System.Collections.ObjectModel.ReadOnlyCollection<OdfPivotTableSortFieldInfo> ParseSortFields(OdfNode pivotTableNode)
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

    private static System.Collections.ObjectModel.ReadOnlyCollection<OdfPivotTableFilterConditionInfo> ParseFilterConditions(OdfNode pivotTableNode)
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

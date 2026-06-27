using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表資料庫範圍讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentDatabaseRangeReadEngine
{
    internal static IReadOnlyList<OdfDatabaseRangeInfo> GetDatabaseRanges(SpreadsheetDocument document)
    {
        OdfNode? databaseRangesNode = OdfTableSheetDomHelper.FindChildElement(
            document.SheetsRoot, "database-ranges", OdfNamespaces.Table);
        if (databaseRangesNode is null)
            return [];

        List<OdfDatabaseRangeInfo> ranges = [];
        foreach (OdfNode child in databaseRangesNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "database-range" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            string? name = child.GetAttribute("name", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(name))
                continue;

            string targetRange = child.GetAttribute("target-range-address", OdfNamespaces.Table) ?? string.Empty;
            ranges.Add(new OdfDatabaseRangeInfo(
                name!,
                targetRange,
                child.GetAttribute("display-filter-buttons", OdfNamespaces.Table) == "true",
                ParseFilterConditions(child),
                ParseSortRules(child)));
        }

        return ranges.AsReadOnly();
    }

    private static IReadOnlyList<OdfDatabaseFilterConditionInfo> ParseFilterConditions(OdfNode databaseRangeNode)
    {
        List<OdfDatabaseFilterConditionInfo> conditions = [];

        foreach (OdfNode child in databaseRangeNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "filter" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            foreach (OdfNode conditionNode in child.Children)
            {
                if (conditionNode.NodeType is not OdfNodeType.Element ||
                    conditionNode.LocalName is not "filter-condition" ||
                    conditionNode.NamespaceUri != OdfNamespaces.Table)
                    continue;

                string? fieldNumberText = conditionNode.GetAttribute("field-number", OdfNamespaces.Table);
                if (!int.TryParse(fieldNumberText, out int fieldNumber))
                    continue;

                conditions.Add(new OdfDatabaseFilterConditionInfo(
                    fieldNumber,
                    conditionNode.GetAttribute("operator", OdfNamespaces.Table) ?? string.Empty,
                    conditionNode.GetAttribute("value", OdfNamespaces.Table) ?? string.Empty));
            }
        }

        return conditions.AsReadOnly();
    }

    private static IReadOnlyList<OdfDatabaseSortRuleInfo> ParseSortRules(OdfNode databaseRangeNode)
    {
        List<OdfDatabaseSortRuleInfo> rules = [];

        foreach (OdfNode child in databaseRangeNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "sort" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            foreach (OdfNode sortByNode in child.Children)
            {
                if (sortByNode.NodeType is not OdfNodeType.Element ||
                    sortByNode.LocalName is not "sort-by" ||
                    sortByNode.NamespaceUri != OdfNamespaces.Table)
                    continue;

                string? fieldNumberText = sortByNode.GetAttribute("field-number", OdfNamespaces.Table);
                if (!int.TryParse(fieldNumberText, out int fieldNumber))
                    continue;

                bool ascending = sortByNode.GetAttribute("order", OdfNamespaces.Table) != "descending";
                rules.Add(new OdfDatabaseSortRuleInfo(fieldNumber, ascending));
            }
        }

        return rules.AsReadOnly();
    }
}

using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表命名範圍與具名運算式讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentNamedRangeReadEngine
{
    internal static IReadOnlyList<OdfNamedRangeInfo> GetNamedRanges(SpreadsheetDocument document)
    {
        List<OdfNamedRangeInfo> ranges = [];
        CollectNamedRanges(document.SheetsRoot, ranges);

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            foreach (OdfNamedRangeInfo range in sheet.NamedRanges)
                ranges.Add(range);
        }

        return ranges.AsReadOnly();
    }

    internal static IReadOnlyList<OdfNamedExpressionInfo> GetNamedExpressions(SpreadsheetDocument document)
    {
        List<OdfNamedExpressionInfo> expressions = [];
        CollectNamedExpressions(document.SheetsRoot, expressions);

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            foreach (OdfNamedExpressionInfo expression in sheet.NamedExpressions)
                expressions.Add(expression);
        }

        return expressions.AsReadOnly();
    }

    private static void CollectNamedRanges(OdfNode parent, List<OdfNamedRangeInfo> ranges)
    {
        OdfNode? namedExpressions = OdfTableSheetDomHelper.FindChildElement(
            parent, "named-expressions", OdfNamespaces.Table);
        if (namedExpressions is null)
            return;

        foreach (OdfNode child in namedExpressions.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "named-range" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
            string address = child.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty;
            string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
            ranges.Add(new OdfNamedRangeInfo(name, address, baseAddress));
        }
    }

    private static void CollectNamedExpressions(OdfNode parent, List<OdfNamedExpressionInfo> expressions)
    {
        OdfNode? namedExpressions = OdfTableSheetDomHelper.FindChildElement(
            parent, "named-expressions", OdfNamespaces.Table);
        if (namedExpressions is null)
            return;

        foreach (OdfNode child in namedExpressions.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "named-expression" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
            string expression = child.GetAttribute("expression", OdfNamespaces.Table) ?? string.Empty;
            string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
            expressions.Add(new OdfNamedExpressionInfo(name, expression, baseAddress));
        }
    }
}

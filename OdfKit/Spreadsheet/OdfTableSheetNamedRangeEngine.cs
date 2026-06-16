using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表命名範圍與具名運算式引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetNamedRangeEngine
{
    internal static void AddNamedRange(
        OdfTableSheetMutationContext context,
        string name,
        OdfCellRange range,
        OdfCellAddress? baseCell)
    {
        var namedExpressions = OdfTableSheetDomHelper.FindOrCreateChild(
            context.TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
        namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        if (baseCell.HasValue)
            namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        namedExpressions.AppendChild(namedRange);
    }

    internal static IReadOnlyList<OdfNamedRangeInfo> GetNamedRanges(OdfTableSheetMutationContext context)
    {
        OdfNode? namedExpressions = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "named-expressions", OdfNamespaces.Table);
        if (namedExpressions is null)
            return [];

        List<OdfNamedRangeInfo> ranges = [];
        foreach (OdfNode child in namedExpressions.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "named-range" &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                string n = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                string address = child.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty;
                string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                ranges.Add(new OdfNamedRangeInfo(n, address, baseAddress));
            }
        }
        return ranges.AsReadOnly();
    }

    internal static void AddNamedExpression(
        OdfTableSheetMutationContext context,
        string name,
        string expression,
        OdfCellAddress? baseCell)
    {
        var namedExpressions = OdfTableSheetDomHelper.FindOrCreateChild(
            context.TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
        namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
        if (baseCell.HasValue)
            namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        namedExpressions.AppendChild(namedExpr);
    }

    internal static IReadOnlyList<OdfNamedExpressionInfo> GetNamedExpressions(OdfTableSheetMutationContext context)
    {
        OdfNode? namedExpressions = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "named-expressions", OdfNamespaces.Table);
        if (namedExpressions is null)
            return [];

        List<OdfNamedExpressionInfo> expressions = [];
        foreach (OdfNode child in namedExpressions.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "named-expression" &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                string n = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                string expr = child.GetAttribute("expression", OdfNamespaces.Table) ?? string.Empty;
                string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                expressions.Add(new OdfNamedExpressionInfo(n, expr, baseAddress));
            }
        }
        return expressions.AsReadOnly();
    }
}

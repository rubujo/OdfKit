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

    internal static OdfNamedRangeInfo? FindNamedRange(OdfTableSheetMutationContext context, string name) =>
        FindNamedItem(context, "named-range", name) is { } node
            ? new OdfNamedRangeInfo(
                node.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty,
                node.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty,
                node.GetAttribute("base-cell-address", OdfNamespaces.Table))
            : null;

    internal static bool RemoveNamedRange(OdfTableSheetMutationContext context, string name) =>
        RemoveNamedItem(context, "named-range", name);

    internal static int ClearNamedRanges(OdfTableSheetMutationContext context) =>
        ClearNamedItems(context, "named-range");

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

    internal static OdfNamedExpressionInfo? FindNamedExpression(OdfTableSheetMutationContext context, string name) =>
        FindNamedItem(context, "named-expression", name) is { } node
            ? new OdfNamedExpressionInfo(
                node.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty,
                node.GetAttribute("expression", OdfNamespaces.Table) ?? string.Empty,
                node.GetAttribute("base-cell-address", OdfNamespaces.Table))
            : null;

    internal static bool RemoveNamedExpression(OdfTableSheetMutationContext context, string name) =>
        RemoveNamedItem(context, "named-expression", name);

    internal static int ClearNamedExpressions(OdfTableSheetMutationContext context) =>
        ClearNamedItems(context, "named-expression");

    private static OdfNode? FindNamedItem(OdfTableSheetMutationContext context, string localName, string name)
    {
        OdfNode? container = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "named-expressions", OdfNamespaces.Table);
        if (container is null)
            return null;

        foreach (OdfNode child in container.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Table &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Table), name, System.StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static bool RemoveNamedItem(OdfTableSheetMutationContext context, string localName, string name)
    {
        OdfNode? node = FindNamedItem(context, localName, name);
        if (node?.Parent is null)
            return false;

        node.Parent.RemoveChild(node);
        return true;
    }

    private static int ClearNamedItems(OdfTableSheetMutationContext context, string localName)
    {
        OdfNode? container = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "named-expressions", OdfNamespaces.Table);
        if (container is null)
            return 0;

        List<OdfNode> matches = [];
        foreach (OdfNode child in container.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                matches.Add(child);
            }
        }

        foreach (OdfNode match in matches)
            container.RemoveChild(match);
        return matches.Count;
    }
}

using System;
using System.IO;
using System.Threading;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides persisted pivot refresh operations for spreadsheet documents.
/// 提供試算表文件的已持久化樞紐刷新操作。
/// </summary>
public partial class SpreadsheetDocument
{
    /// <summary>
    /// Refreshes a persisted pivot table with default resource limits.
    /// 使用預設資源限制刷新已持久化的樞紐分析表。
    /// </summary>
    /// <param name="name">The exact pivot table name. / 樞紐分析表的精確名稱。</param>
    /// <returns>The refresh report. / 刷新報告。</returns>
    public OdfPivotRefreshResult RefreshPivotTable(string name) =>
        RefreshPivotTable(name, null, CancellationToken.None);

    /// <summary>
    /// Refreshes a persisted pivot table with bounded resource use.
    /// 以有界資源使用刷新已持久化的樞紐分析表。
    /// </summary>
    /// <param name="name">The exact pivot table name. / 樞紐分析表的精確名稱。</param>
    /// <param name="options">The resource limits, or <see langword="null"/> for defaults. / 資源限制；<see langword="null"/> 表示預設值。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The refresh report. / 刷新報告。</returns>
    public OdfPivotRefreshResult RefreshPivotTable(
        string name,
        OdfPivotRefreshOptions? options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(name, nameof(name));
        OdfPivotTableInfo? info = null;
        foreach (OdfPivotTableInfo candidate in GetPivotTables())
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                info = candidate;
                break;
            }
        }
        if (info is null ||
            !info.TryGetSourceRange(out OdfCellRange sourceRange) ||
            !info.TryGetTargetStart(out OdfCellAddress targetStart))
        {
            throw new InvalidDataException();
        }

        string targetSheetName = targetStart.SheetName ?? info.SheetName;
        if (string.IsNullOrWhiteSpace(targetSheetName))
            throw new InvalidDataException();
        OdfTableSheet targetSheet = Worksheets[targetSheetName];
        var builder = new OdfPivotTableBuilder(name, sourceRange, targetStart, targetSheet);
        foreach (OdfPivotTableFieldInfo field in info.Fields)
        {
            switch (field.Orientation)
            {
                case "row":
                    builder.AddRowField(field.SourceFieldName);
                    break;
                case "column":
                    builder.AddColumnField(field.SourceFieldName);
                    break;
                case "page":
                    builder.AddPageField(field.SourceFieldName);
                    break;
                case "data" when string.Equals(field.Function, "formula", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(field.Formula):
                    builder.AddCalculatedField(field.SourceFieldName, field.Formula!);
                    break;
                case "data":
                    builder.AddDataField(field.SourceFieldName, field.Function ?? "sum");
                    break;
            }
        }
        foreach (OdfPivotTableSortFieldInfo sort in info.SortFields)
            builder.AddSortInfo(sort.SourceFieldName, sort.Ascending);
        foreach (OdfPivotTableFilterConditionInfo filter in info.FilterConditions)
        {
            builder.AddFilter(
                filter.SourceFieldName,
                ParseFilterOperator(filter.Operator),
                filter.Value);
        }

        OdfNode node = FindPivotTableNode(name) ?? throw new InvalidDataException();
        builder
            .WithGrandTotals(ParseGrandTotal(node.GetAttribute("grand-total", OdfNamespaces.Table)))
            .WithFilterButton(node.GetAttribute("show-filter-button", OdfNamespaces.Table) != "false")
            .WithDrillDown(node.GetAttribute("drill-down-on-double-click", OdfNamespaces.Table) == "true");
        ApplyPersistedAdvancedOptions(builder, node);
        return builder.Refresh(options, cancellationToken);
    }

    private static void ApplyPersistedAdvancedOptions(OdfPivotTableBuilder builder, OdfNode pivotNode)
    {
        foreach (OdfNode fieldNode in pivotNode.Children)
        {
            if (fieldNode.NodeType is not OdfNodeType.Element ||
                fieldNode.LocalName != "data-pilot-field" ||
                fieldNode.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }
            string? fieldName = fieldNode.GetAttribute("source-field-name", OdfNamespaces.Table);
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;
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
                            builder.WithLayout(ParseLayout(
                                levelChild.GetAttribute("layout-mode", OdfNamespaces.Table)));
                        }
                    }
                }
                else if (child.LocalName == "data-pilot-groups")
                {
                    builder.GroupField(fieldName!, ParseGrouping(child));
                }
                else if (child.LocalName == "data-pilot-field-reference")
                {
                    builder.ConfigureValueField(fieldName!, new OdfPivotValueOptions
                    {
                        ShowValuesAs = ParseShowValuesAs(
                            child.GetAttribute("type", OdfNamespaces.Table)),
                        BaseFieldName = child.GetAttribute("field-name", OdfNamespaces.Table),
                        BaseMemberName = child.GetAttribute("member-name", OdfNamespaces.Table),
                    });
                }
            }
        }
    }

    private static OdfPivotGroupingOptions ParseGrouping(OdfNode node)
    {
        if (!string.IsNullOrEmpty(node.GetAttribute("start", OdfNamespaces.Table)))
        {
            return new OdfPivotGroupingOptions
            {
                Start = ParseFiniteDouble(node, "start"),
                End = ParseFiniteDouble(node, "end"),
                Interval = ParseFiniteDouble(node, "step"),
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
                    _ => throw new InvalidDataException(),
                },
            };
        }
        throw new InvalidDataException();
    }

    private static double ParseFiniteDouble(OdfNode node, string attributeName)
    {
        string? text = node.GetAttribute(attributeName, OdfNamespaces.Table);
        if (!double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double value) ||
            double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            throw new InvalidDataException();
        }
        return value;
    }

    private static OdfPivotFilterOperator ParseFilterOperator(string value) => value switch
    {
        "=" => OdfPivotFilterOperator.Equal,
        "!=" => OdfPivotFilterOperator.NotEqual,
        ">" => OdfPivotFilterOperator.GreaterThan,
        ">=" => OdfPivotFilterOperator.GreaterThanOrEqual,
        "<" => OdfPivotFilterOperator.LessThan,
        "<=" => OdfPivotFilterOperator.LessThanOrEqual,
        _ => throw new InvalidDataException(),
    };

    private static OdfPivotGrandTotal ParseGrandTotal(string? value) => value switch
    {
        null or "" or "none" => OdfPivotGrandTotal.None,
        "row" => OdfPivotGrandTotal.Row,
        "column" => OdfPivotGrandTotal.Column,
        "both" => OdfPivotGrandTotal.Both,
        _ => throw new InvalidDataException(),
    };

    private static OdfPivotLayout ParseLayout(string? value) => value switch
    {
        null or "" or "tabular-layout" => OdfPivotLayout.Tabular,
        "outline-subtotals-bottom" => OdfPivotLayout.OutlineSubtotalsBottom,
        "outline-subtotals-top" => OdfPivotLayout.OutlineSubtotalsTop,
        _ => throw new InvalidDataException(),
    };

    private static OdfPivotShowValuesAs ParseShowValuesAs(string? value) => value switch
    {
        null or "" or "none" => OdfPivotShowValuesAs.None,
        "row-percentage" => OdfPivotShowValuesAs.PercentageOfRowTotal,
        "column-percentage" => OdfPivotShowValuesAs.PercentageOfColumnTotal,
        "total-percentage" => OdfPivotShowValuesAs.PercentageOfGrandTotal,
        "running-total" => OdfPivotShowValuesAs.RunningTotal,
        "member-difference" => OdfPivotShowValuesAs.DifferenceFrom,
        "member-percentage-difference" => OdfPivotShowValuesAs.PercentageDifferenceFrom,
        "index" => OdfPivotShowValuesAs.Index,
        _ => throw new InvalidDataException(),
    };
}

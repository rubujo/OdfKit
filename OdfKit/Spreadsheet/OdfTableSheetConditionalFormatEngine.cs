using System;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表條件格式引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetConditionalFormatEngine
{
    /// <summary>
    /// 新增條件格式。
    /// </summary>
    internal static void AddConditionalFormat(
        OdfTableSheetMutationContext context,
        OdfCellRange range,
        string conditionValue,
        string styleName)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats(context.TableNode);

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        format.SetAttribute("target-range-address", calcextNs, BuildConditionalRangeAddr(context.SheetName, range), "calcext");

        var condition = new OdfNode(OdfNodeType.Element, "condition", calcextNs, "calcext");
        condition.SetAttribute("value", calcextNs, conditionValue, "calcext");
        condition.SetAttribute("style-name", calcextNs, styleName, "calcext");
        format.AppendChild(condition);

        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 新增色階條件格式（兩色或三色）。
    /// </summary>
    internal static void AddColorScaleFormat(
        OdfTableSheetMutationContext context,
        OdfCellRange range,
        OdfColor minColor,
        OdfColor maxColor,
        OdfColor? midColor)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats(context.TableNode);
        string rangeAddr = BuildConditionalRangeAddr(context.SheetName, range);

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

        var colorScale = new OdfNode(OdfNodeType.Element, "color-scale", calcextNs, "calcext");

        var entryMin = new OdfNode(OdfNodeType.Element, "color-scale-entry", calcextNs, "calcext");
        entryMin.SetAttribute("type", calcextNs, "min", "calcext");
        entryMin.SetAttribute("color", calcextNs, minColor.Value, "calcext");
        colorScale.AppendChild(entryMin);

        if (midColor.HasValue)
        {
            var entryMid = new OdfNode(OdfNodeType.Element, "color-scale-entry", calcextNs, "calcext");
            entryMid.SetAttribute("type", calcextNs, "percentile", "calcext");
            entryMid.SetAttribute("value", calcextNs, "50", "calcext");
            entryMid.SetAttribute("color", calcextNs, midColor.Value.Value, "calcext");
            colorScale.AppendChild(entryMid);
        }

        var entryMax = new OdfNode(OdfNodeType.Element, "color-scale-entry", calcextNs, "calcext");
        entryMax.SetAttribute("type", calcextNs, "max", "calcext");
        entryMax.SetAttribute("color", calcextNs, maxColor.Value, "calcext");
        colorScale.AppendChild(entryMax);

        format.AppendChild(colorScale);
        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 新增資料橫條條件格式。
    /// </summary>
    internal static void AddDataBarFormat(
        OdfTableSheetMutationContext context,
        OdfCellRange range,
        OdfColor positiveColor,
        OdfColor? negativeColor)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats(context.TableNode);
        string rangeAddr = BuildConditionalRangeAddr(context.SheetName, range);

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

        var dataBar = new OdfNode(OdfNodeType.Element, "data-bar", calcextNs, "calcext");
        dataBar.SetAttribute("positive-color", calcextNs, positiveColor.Value, "calcext");
        if (negativeColor.HasValue)
            dataBar.SetAttribute("negative-color", calcextNs, negativeColor.Value.Value, "calcext");

        format.AppendChild(dataBar);
        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 新增圖示集條件格式。
    /// </summary>
    internal static void AddIconSetFormat(
        OdfTableSheetMutationContext context,
        OdfCellRange range,
        OdfIconSetType iconSet)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats(context.TableNode);
        string rangeAddr = BuildConditionalRangeAddr(context.SheetName, range);

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

        string iconTypeName = iconSet switch
        {
            OdfIconSetType.ThreeArrows => "3Arrows",
            OdfIconSetType.ThreeTrafficLights => "3TrafficLights1",
            OdfIconSetType.FourRating => "4Rating",
            OdfIconSetType.FiveRating => "5Rating",
            _ => "3Arrows",
        };
        int entryCount = iconSet is OdfIconSetType.FiveRating ? 5
                       : iconSet is OdfIconSetType.FourRating ? 4
                       : 3;

        var iconSetNode = new OdfNode(OdfNodeType.Element, "icon-set", calcextNs, "calcext");
        iconSetNode.SetAttribute("icon-set-type", calcextNs, iconTypeName, "calcext");

        for (int i = 0; i < entryCount; i++)
        {
            int pct = i == 0 ? 0 : (int)Math.Round(100.0 * i / entryCount);
            var entry = new OdfNode(OdfNodeType.Element, "icon-set-entry", calcextNs, "calcext");
            entry.SetAttribute("type", calcextNs, "percent", "calcext");
            entry.SetAttribute("value", calcextNs, pct.ToString(), "calcext");
            iconSetNode.AppendChild(entry);
        }

        format.AppendChild(iconSetNode);
        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 在工作表中新增 LibreOffice calcext 走勢圖群組。
    /// </summary>
    internal static void AddSparklineGroup(
        OdfTableSheetMutationContext context,
        OdfCellRange dataRange,
        OdfCellAddress hostCell,
        SparklineType type)
    {
        const string calcextNs = OdfNamespaces.CalcExt;

        OdfNode? groupsNode = null;
        foreach (var child in context.TableNode.Children)
        {
            if (child.LocalName == "sparkline-groups" && child.NamespaceUri == calcextNs)
            {
                groupsNode = child;
                break;
            }
        }
        if (groupsNode is null)
        {
            groupsNode = new OdfNode(OdfNodeType.Element, "sparkline-groups", calcextNs, "calcext");
            context.TableNode.AppendChild(groupsNode);
        }

        var groupNode = new OdfNode(OdfNodeType.Element, "sparkline-group", calcextNs, "calcext");
        groupNode.SetAttribute("type", calcextNs, SparklineTypeToString(type), "calcext");
        groupsNode.AppendChild(groupNode);

        var sparklinesNode = new OdfNode(OdfNodeType.Element, "sparklines", calcextNs, "calcext");
        groupNode.AppendChild(sparklinesNode);

        var startAddr = dataRange.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, context.SheetName,
                startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = dataRange.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, context.SheetName,
                endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);
        var host = hostCell.SheetName is null
            ? new OdfCellAddress(hostCell.Row, hostCell.Column, context.SheetName, true, true, true)
            : hostCell;

        var sparklineNode = new OdfNode(OdfNodeType.Element, "sparkline", calcextNs, "calcext");
        sparklineNode.SetAttribute("dataRangeRef", calcextNs,
            $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}", "calcext");
        sparklineNode.SetAttribute("hostCellRef", calcextNs, host.ToOdfString(false), "calcext");
        sparklinesNode.AppendChild(sparklineNode);
    }

    private static OdfNode FindOrCreateCalcExtConditionalFormats(OdfNode tableNode)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        foreach (var child in tableNode.Children)
        {
            if (child.LocalName == "conditional-formats" && child.NamespaceUri == calcextNs)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, "conditional-formats", calcextNs, "calcext");
        tableNode.AppendChild(node);
        return node;
    }

    private static string BuildConditionalRangeAddr(string sheetName, OdfCellRange range)
    {
        var startAddr = range.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, sheetName,
                startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = range.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, sheetName,
                endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);
        return $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}";
    }

    private static string SparklineTypeToString(SparklineType type) => type switch
    {
        SparklineType.Column => "column",
        SparklineType.WinLoss => "stacked",
        _ => "line"
    };
}

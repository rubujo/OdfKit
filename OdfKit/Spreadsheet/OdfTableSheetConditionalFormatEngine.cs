using System;
using System.Collections.Generic;
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
    /// 列舉工作表中的 LibreOffice calcext 條件格式規則。
    /// </summary>
    internal static IReadOnlyList<OdfConditionalFormatInfo> GetConditionalFormats(
        OdfTableSheetMutationContext context)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode? formatsNode = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "conditional-formats", calcextNs);
        if (formatsNode is null)
            return [];

        List<OdfConditionalFormatInfo> formats = [];
        foreach (OdfNode child in formatsNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "conditional-format" ||
                child.NamespaceUri != calcextNs)
                continue;

            string targetRange = child.GetAttribute("target-range-address", calcextNs) ?? string.Empty;
            OdfConditionalFormatInfo? info = TryParseConditionalFormat(child, targetRange, calcextNs);
            if (info is not null)
                formats.Add(info);
        }

        return formats.AsReadOnly();
    }

    /// <summary>
    /// 列舉工作表中的 LibreOffice calcext 走勢圖群組。
    /// </summary>
    internal static IReadOnlyList<OdfSparklineGroupInfo> GetSparklineGroups(
        OdfTableSheetMutationContext context)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode? groupsNode = OdfTableSheetDomHelper.FindChildElement(
            context.TableNode, "sparkline-groups", calcextNs);
        if (groupsNode is null)
            return [];

        List<OdfSparklineGroupInfo> groups = [];
        foreach (OdfNode groupNode in groupsNode.Children)
        {
            if (groupNode.NodeType is not OdfNodeType.Element ||
                groupNode.LocalName is not "sparkline-group" ||
                groupNode.NamespaceUri != calcextNs)
                continue;

            string typeAttr = groupNode.GetAttribute("type", calcextNs) ?? "line";
            SparklineType type = ParseSparklineType(typeAttr);
            List<OdfSparklineInfo> sparklines = [];

            foreach (OdfNode child in groupNode.Children)
            {
                if (child.NodeType is not OdfNodeType.Element ||
                    child.LocalName is not "sparklines" ||
                    child.NamespaceUri != calcextNs)
                    continue;

                foreach (OdfNode sparklineNode in child.Children)
                {
                    if (sparklineNode.NodeType is not OdfNodeType.Element ||
                        sparklineNode.LocalName is not "sparkline" ||
                        sparklineNode.NamespaceUri != calcextNs)
                        continue;

                    string dataRangeRef = sparklineNode.GetAttribute("dataRangeRef", calcextNs) ?? string.Empty;
                    string hostCellRef = sparklineNode.GetAttribute("hostCellRef", calcextNs) ?? string.Empty;
                    sparklines.Add(new OdfSparklineInfo(dataRangeRef, hostCellRef));
                }
            }

            groups.Add(new OdfSparklineGroupInfo(type, sparklines.AsReadOnly()));
        }

        return groups.AsReadOnly();
    }
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
        entryMin.SetAttribute("value", calcextNs, "0", "calcext");
        entryMin.SetAttribute("type", calcextNs, "minimum", "calcext");
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
        entryMax.SetAttribute("value", calcextNs, "0", "calcext");
        entryMax.SetAttribute("type", calcextNs, "maximum", "calcext");
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
        dataBar.SetAttribute("max-length", calcextNs, "100", "calcext");
        dataBar.SetAttribute("negative-color", calcextNs,
            negativeColor.HasValue ? negativeColor.Value.Value : "#ff0000", "calcext");
        dataBar.SetAttribute("positive-color", calcextNs, positiveColor.Value, "calcext");
        dataBar.SetAttribute("axis-color", calcextNs, "#000000", "calcext");

        // calcext:data-bar 須帶 auto-minimum／auto-maximum 兩個 calcext:formatting-entry 子節點，
        // 否則 LibreOffice 無法正確依資料範圍實際最小/最大值計算長條比例（已以真實 LibreOffice 內建
        // 說明文件範例檔 conditionalformatting.ods 驗證；缺少時所有非最小值的長條會被錯誤地畫滿整格）。
        var minEntry = new OdfNode(OdfNodeType.Element, "formatting-entry", calcextNs, "calcext");
        minEntry.SetAttribute("value", calcextNs, "0", "calcext");
        minEntry.SetAttribute("type", calcextNs, "auto-minimum", "calcext");
        dataBar.AppendChild(minEntry);

        var maxEntry = new OdfNode(OdfNodeType.Element, "formatting-entry", calcextNs, "calcext");
        maxEntry.SetAttribute("value", calcextNs, "0", "calcext");
        maxEntry.SetAttribute("type", calcextNs, "auto-maximum", "calcext");
        dataBar.AppendChild(maxEntry);

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

        // 子節點標籤名稱須為 calcext:formatting-entry，不是 calcext:icon-set-entry（已以真實
        // LibreOffice 內建說明文件範例檔 conditionalformatting.ods 驗證；先前的錯誤標籤名稱會讓
        // LibreOffice 完全不顯示任何圖示）。
        for (int i = 0; i < entryCount; i++)
        {
            int pct = i == 0 ? 0 : (int)Math.Round(100.0 * i / entryCount);
            var entry = new OdfNode(OdfNodeType.Element, "formatting-entry", calcextNs, "calcext");
            entry.SetAttribute("value", calcextNs, pct.ToString(), "calcext");
            entry.SetAttribute("type", calcextNs, "percent", "calcext");
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

    private static SparklineType ParseSparklineType(string value) => value switch
    {
        "column" => SparklineType.Column,
        "stacked" => SparklineType.WinLoss,
        _ => SparklineType.Line
    };

    private static OdfConditionalFormatInfo? TryParseConditionalFormat(
        OdfNode formatNode, string targetRange, string calcextNs)
    {
        foreach (OdfNode child in formatNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != calcextNs)
                continue;

            switch (child.LocalName)
            {
                case "condition":
                    return new OdfConditionalFormatInfo(
                        OdfConditionalFormatKind.Condition,
                        targetRange,
                        conditionValue: child.GetAttribute("value", calcextNs),
                        styleName: child.GetAttribute("style-name", calcextNs));

                case "color-scale":
                    return ParseColorScaleFormat(targetRange, child, calcextNs);

                case "data-bar":
                    return ParseDataBarFormat(targetRange, child, calcextNs);

                case "icon-set":
                    return ParseIconSetFormat(targetRange, child, calcextNs);
            }
        }

        return null;
    }

    private static OdfConditionalFormatInfo ParseColorScaleFormat(
        string targetRange, OdfNode colorScaleNode, string calcextNs)
    {
        OdfColor? minColor = null;
        OdfColor? maxColor = null;
        OdfColor? midColor = null;

        foreach (OdfNode entry in colorScaleNode.Children)
        {
            if (entry.NodeType is not OdfNodeType.Element ||
                entry.LocalName is not "color-scale-entry" ||
                entry.NamespaceUri != calcextNs)
                continue;

            string entryType = entry.GetAttribute("type", calcextNs) ?? string.Empty;
            OdfColor? color = OdfElementComplexAttributeAccess.GetColor(
                entry.GetAttribute("color", calcextNs));

            switch (entryType)
            {
                case "minimum":
                    minColor = color;
                    break;
                case "maximum":
                    maxColor = color;
                    break;
                case "percentile":
                    midColor = color;
                    break;
            }
        }

        return new OdfConditionalFormatInfo(
            OdfConditionalFormatKind.ColorScale,
            targetRange,
            minColor: minColor,
            maxColor: maxColor,
            midColor: midColor);
    }

    private static OdfConditionalFormatInfo ParseDataBarFormat(
        string targetRange, OdfNode dataBarNode, string calcextNs) =>
        new(
            OdfConditionalFormatKind.DataBar,
            targetRange,
            positiveColor: OdfElementComplexAttributeAccess.GetColor(
                dataBarNode.GetAttribute("positive-color", calcextNs)),
            negativeColor: OdfElementComplexAttributeAccess.GetColor(
                dataBarNode.GetAttribute("negative-color", calcextNs)));

    private static OdfConditionalFormatInfo ParseIconSetFormat(
        string targetRange, OdfNode iconSetNode, string calcextNs)
    {
        string? typeName = iconSetNode.GetAttribute("icon-set-type", calcextNs);
        OdfIconSetType? iconSetType = TryParseIconSetType(typeName);
        return new OdfConditionalFormatInfo(
            OdfConditionalFormatKind.IconSet,
            targetRange,
            iconSetTypeName: typeName,
            iconSetType: iconSetType);
    }

    private static OdfIconSetType? TryParseIconSetType(string? typeName) => typeName switch
    {
        "3Arrows" => OdfIconSetType.ThreeArrows,
        "3TrafficLights1" => OdfIconSetType.ThreeTrafficLights,
        "4Rating" => OdfIconSetType.FourRating,
        "5Rating" => OdfIconSetType.FiveRating,
        _ => null
    };
}

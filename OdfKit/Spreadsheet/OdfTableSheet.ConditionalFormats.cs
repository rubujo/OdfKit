using System;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region ConditionalFormats

    /// <summary>
    /// 新增條件格式。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <param name="conditionValue">條件運算式</param>
    /// <param name="styleName">要套用的格式樣式名稱</param>
    public void AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName)
    {
        const string calcextNs = OdfNamespaces.CalcExt;

        OdfNode? formatsNode = null;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "conditional-formats" && child.NamespaceUri == calcextNs)
            {
                formatsNode = child;
                break;
            }
        }
        if (formatsNode is null)
        {
            formatsNode = new OdfNode(OdfNodeType.Element, "conditional-formats", calcextNs, "calcext");
            TableNode.AppendChild(formatsNode);
        }

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        var startAddr = range.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name, startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = range.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name, endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);

        string rangeAddr = $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}";
        format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

        var condition = new OdfNode(OdfNodeType.Element, "condition", calcextNs, "calcext");
        condition.SetAttribute("value", calcextNs, conditionValue, "calcext");
        condition.SetAttribute("style-name", calcextNs, styleName, "calcext");
        format.AppendChild(condition);

        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 新增色階條件格式（兩色或三色）。
    /// </summary>
    /// <param name="range">套用範圍。</param>
    /// <param name="minColor">最小值對應色彩。</param>
    /// <param name="maxColor">最大值對應色彩。</param>
    /// <param name="midColor">中間值對應色彩（可選，設定時為三色色階）。</param>
    public void AddColorScaleFormat(OdfCellRange range,
        OdfColor minColor, OdfColor maxColor, OdfColor? midColor = null)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats();
        string rangeAddr = BuildConditionalRangeAddr(range);

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
    /// <param name="range">套用範圍。</param>
    /// <param name="positiveColor">正值橫條色彩。</param>
    /// <param name="negativeColor">負值橫條色彩（可選）。</param>
    public void AddDataBarFormat(OdfCellRange range,
        OdfColor positiveColor, OdfColor? negativeColor = null)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats();
        string rangeAddr = BuildConditionalRangeAddr(range);

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
    /// <param name="range">套用範圍。</param>
    /// <param name="iconSet">圖示集類型。</param>
    public void AddIconSetFormat(OdfCellRange range, OdfIconSetType iconSet)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        OdfNode formatsNode = FindOrCreateCalcExtConditionalFormats();
        string rangeAddr = BuildConditionalRangeAddr(range);

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

    private OdfNode FindOrCreateCalcExtConditionalFormats()
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "conditional-formats" && child.NamespaceUri == calcextNs)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, "conditional-formats", calcextNs, "calcext");
        TableNode.AppendChild(node);
        return node;
    }

    private string BuildConditionalRangeAddr(OdfCellRange range)
    {
        var startAddr = range.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name,
                startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = range.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name,
                endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);
        return $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}";
    }

    /// <summary>
    /// 在工作表中新增 LibreOffice calcext 走勢圖群組。
    /// </summary>
    /// <param name="dataRange">走勢圖資料來源範圍。</param>
    /// <param name="hostCell">顯示走勢圖的儲存格位址。</param>
    /// <param name="type">走勢圖類型，預設為折線。</param>
    /// <exception cref="ArgumentNullException">當 dataRange 為 null 時拋出。</exception>
    public void AddSparklineGroup(OdfCellRange? dataRange, OdfCellAddress hostCell, SparklineType type = SparklineType.Line)
    {
        if (dataRange is null)
            throw new ArgumentNullException(nameof(dataRange));

        const string calcextNs = OdfNamespaces.CalcExt;

        OdfNode? groupsNode = null;
        foreach (var child in TableNode.Children)
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
            TableNode.AppendChild(groupsNode);
        }

        var groupNode = new OdfNode(OdfNodeType.Element, "sparkline-group", calcextNs, "calcext");
        groupNode.SetAttribute("type", calcextNs, SparklineTypeToString(type), "calcext");
        groupsNode.AppendChild(groupNode);

        var sparklinesNode = new OdfNode(OdfNodeType.Element, "sparklines", calcextNs, "calcext");
        groupNode.AppendChild(sparklinesNode);

        var startAddr = dataRange.Value.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name,
                startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = dataRange.Value.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name,
                endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);
        var host = hostCell.SheetName is null
            ? new OdfCellAddress(hostCell.Row, hostCell.Column, Name, true, true, true)
            : hostCell;

        var sparklineNode = new OdfNode(OdfNodeType.Element, "sparkline", calcextNs, "calcext");
        sparklineNode.SetAttribute("dataRangeRef", calcextNs,
            $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}", "calcext");
        sparklineNode.SetAttribute("hostCellRef", calcextNs, host.ToOdfString(false), "calcext");
        sparklinesNode.AppendChild(sparklineNode);
    }

    private static string SparklineTypeToString(SparklineType type) => type switch
    {
        SparklineType.Column => "column",
        SparklineType.WinLoss => "stacked",
        _ => "line"
    };

    /// <summary>
    /// 新增資料庫範圍至此工作表。
    /// </summary>
    /// <param name="name">資料庫範圍名稱。</param>
    /// <param name="range">目標儲存格範圍。</param>
    /// <returns>新增的資料庫範圍。</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range)
    {
        return _doc.AddDatabaseRange(name, range);
    }


    #endregion
}

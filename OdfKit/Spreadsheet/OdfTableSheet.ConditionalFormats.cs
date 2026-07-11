using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the OdfTableSheet API.
/// 提供 OdfTableSheet API。
/// </summary>

public partial class OdfTableSheet
{
    #region ConditionalFormats

    /// <summary>
    /// Gets the LibreOffice calcext conditional formatting rules in this worksheet.
    /// 取得此工作表中的 LibreOffice calcext 條件格式規則清單。
    /// </summary>
    public IReadOnlyList<OdfConditionalFormatInfo> ConditionalFormats =>
        OdfTableSheetConditionalFormatEngine.GetConditionalFormats(MutationContext);

    /// <summary>
    /// Gets the LibreOffice calcext sparkline groups in this worksheet.
    /// 取得此工作表中的 LibreOffice calcext 走勢圖群組清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineGroupInfo> SparklineGroups =>
        OdfTableSheetConditionalFormatEngine.GetSparklineGroups(MutationContext);

    /// <summary>
    /// Finds the first conditional format that matches the predicate.
    /// 尋找第一個符合述詞的條件格式。
    /// </summary>
    /// <param name="predicate">The matching predicate. / 比對述詞。</param>
    /// <returns>The matching format, or <see langword="null"/>. / 相符的格式；若不存在則為 <see langword="null"/>。</returns>
    public OdfConditionalFormatInfo? FindConditionalFormat(Predicate<OdfConditionalFormatInfo> predicate)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        return OdfTableSheetConditionalFormatEngine.FindConditionalFormat(MutationContext, predicate);
    }

    /// <summary>
    /// Removes the first conditional format with the same semantic values as the supplied summary.
    /// 移除第一個與指定摘要具有相同語意值的條件格式。
    /// </summary>
    /// <param name="format">The format summary. / 格式摘要。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveConditionalFormat(OdfConditionalFormatInfo format)
    {
        if (format is null)
            throw new ArgumentNullException(nameof(format));
        return OdfTableSheetConditionalFormatEngine.RemoveConditionalFormat(MutationContext, format);
    }

    /// <summary>
    /// Updates the target range of the matching conditional format while preserving its rule and unknown content.
    /// 更新相符條件格式的目標範圍，並保留其規則與未知內容。
    /// </summary>
    /// <param name="format">The current format summary. / 目前的格式摘要。</param>
    /// <param name="range">The replacement target range. / 取代用目標範圍。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateConditionalFormatRange(OdfConditionalFormatInfo format, OdfCellRange range)
    {
        if (format is null)
            throw new ArgumentNullException(nameof(format));
        return OdfTableSheetConditionalFormatEngine.UpdateConditionalFormatRange(MutationContext, format, range);
    }

    /// <summary>
    /// Removes all conditional formats while preserving unknown content in the container.
    /// 移除所有條件格式，並保留容器中的未知內容。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearConditionalFormats() =>
        OdfTableSheetConditionalFormatEngine.ClearConditionalFormats(MutationContext);

    /// <summary>
    /// Finds the first sparkline group that matches the predicate.
    /// 尋找第一個符合述詞的走勢圖群組。
    /// </summary>
    /// <param name="predicate">The matching predicate. / 比對述詞。</param>
    /// <returns>The matching group, or <see langword="null"/>. / 相符的群組；若不存在則為 <see langword="null"/>。</returns>
    public OdfSparklineGroupInfo? FindSparklineGroup(Predicate<OdfSparklineGroupInfo> predicate)
    {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));
        return OdfTableSheetConditionalFormatEngine.FindSparklineGroup(MutationContext, predicate);
    }

    /// <summary>
    /// Removes the first sparkline group with the same semantic values as the supplied summary.
    /// 移除第一個與指定摘要具有相同語意值的走勢圖群組。
    /// </summary>
    /// <param name="group">The sparkline group summary. / 走勢圖群組摘要。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveSparklineGroup(OdfSparklineGroupInfo group)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));
        return OdfTableSheetConditionalFormatEngine.RemoveSparklineGroup(MutationContext, group);
    }

    /// <summary>
    /// Updates the type of the matching sparkline group while preserving its sparkline references and unknown content.
    /// 更新相符走勢圖群組的類型，並保留其走勢圖引用與未知內容。
    /// </summary>
    /// <param name="group">The current group summary. / 目前的群組摘要。</param>
    /// <param name="type">The replacement sparkline type. / 取代用走勢圖類型。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool UpdateSparklineGroupType(OdfSparklineGroupInfo group, SparklineType type)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));
        return OdfTableSheetConditionalFormatEngine.UpdateSparklineGroupType(MutationContext, group, type);
    }

    /// <summary>
    /// Removes all sparkline groups while preserving unknown content in the container.
    /// 移除所有走勢圖群組，並保留容器中的未知內容。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearSparklineGroups() =>
        OdfTableSheetConditionalFormatEngine.ClearSparklineGroups(MutationContext);

    /// <summary>
    /// Adds a conditional format.
    /// 新增條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="conditionValue">The value to use. / 條件運算式</param>
    /// <param name="styleName">The name or identifier. / 要套用的格式樣式名稱</param>
    public void AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName) =>
        OdfTableSheetConditionalFormatEngine.AddConditionalFormat(
            MutationContext, range, conditionValue, styleName);
    /// <summary>
    /// Short overload of AddColorScaleFormat that accepts range, minColor, and maxColor; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 range、minColor 與 maxColor；其餘可選參數使用預設值並轉呼叫最長 AddColorScaleFormat 多載。
    /// </summary>
    public void AddColorScaleFormat(OdfCellRange range, OdfColor minColor, OdfColor maxColor) => AddColorScaleFormat(range, minColor, maxColor, null);


    /// <summary>
    /// Adds a two-color or three-color scale conditional format.
    /// 新增色階條件格式（兩色或三色）。
    /// </summary>
    /// <param name="range">The cell range. / 套用範圍</param>
    /// <param name="minColor">The numeric value. / 最小值對應色彩</param>
    /// <param name="maxColor">The numeric value. / 最大值對應色彩</param>
    /// <param name="midColor">The numeric value. / 中間值對應色彩（可選，設定時為三色色階）</param>
    public void AddColorScaleFormat(OdfCellRange range, OdfColor minColor, OdfColor maxColor, OdfColor? midColor) =>
        OdfTableSheetConditionalFormatEngine.AddColorScaleFormat(
            MutationContext, range, minColor, maxColor, midColor);

    /// <summary>
    /// Short overload of AddDataBarFormat that accepts range and positiveColor; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 range 與 positiveColor；其餘可選參數使用預設值並轉呼叫最長 AddDataBarFormat 多載。
    /// </summary>
    public void AddDataBarFormat(OdfCellRange range, OdfColor positiveColor) => AddDataBarFormat(range, positiveColor, null);


    /// <summary>
    /// Adds a data bar conditional format.
    /// 新增資料橫條條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 套用範圍</param>
    /// <param name="positiveColor">The numeric value. / 正值橫條色彩</param>
    /// <param name="negativeColor">The numeric value. / 負值橫條色彩（可選）</param>
    public void AddDataBarFormat(OdfCellRange range, OdfColor positiveColor, OdfColor? negativeColor) =>
        OdfTableSheetConditionalFormatEngine.AddDataBarFormat(
            MutationContext, range, positiveColor, negativeColor);

    /// <summary>
    /// Short overload of AddDataBar that accepts range and color; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 range 與 color；其餘可選參數使用預設值並轉呼叫最長 AddDataBar 多載。
    /// </summary>
    public void AddDataBar(OdfCellRange range, OdfColor color) => AddDataBar(range, color, null);


    /// <summary>
    /// Adds a data bar conditional format.
    /// 新增資料橫條條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 套用範圍</param>
    /// <param name="color">The numeric value. / 正值橫條色彩</param>
    /// <param name="negativeColor">The numeric value. / 負值橫條色彩（可選）</param>
    public void AddDataBar(OdfCellRange range, OdfColor color, OdfColor? negativeColor) =>
        AddDataBarFormat(range, color, negativeColor);


    /// <summary>
    /// Adds an icon set conditional format.
    /// 新增圖示集條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 套用範圍</param>
    /// <param name="iconSet">The value to use. / 圖示集類型</param>
    public void AddIconSetFormat(OdfCellRange range, OdfIconSetType iconSet) =>
        OdfTableSheetConditionalFormatEngine.AddIconSetFormat(MutationContext, range, iconSet);

    /// <summary>
    /// Adds an icon set conditional format.
    /// 新增圖示集條件格式。
    /// </summary>
    /// <param name="range">The cell range. / 套用範圍</param>
    /// <param name="iconSet">The value to use. / 圖示集類型</param>
    public void AddIconSet(OdfCellRange range, OdfIconSetType iconSet) =>
        AddIconSetFormat(range, iconSet);
    /// <summary>
    /// Short overload of AddSparklineGroup that accepts dataRange and hostCell; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 dataRange 與 hostCell；其餘可選參數使用預設值並轉呼叫最長 AddSparklineGroup 多載。
    /// </summary>
    public void AddSparklineGroup(OdfCellRange? dataRange, OdfCellAddress hostCell) => AddSparklineGroup(dataRange, hostCell, SparklineType.Line);


    /// <summary>
    /// Adds a LibreOffice calcext sparkline group to the worksheet.
    /// 在工作表中新增 LibreOffice calcext 走勢圖群組。
    /// </summary>
    /// <param name="dataRange">The cell range. / 走勢圖資料來源範圍</param>
    /// <param name="hostCell">The cell address. / 顯示走勢圖的儲存格位址</param>
    /// <param name="type">The value to use. / 走勢圖類型，預設為折線</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 dataRange 為 null 時拋出</exception>
    public void AddSparklineGroup(OdfCellRange? dataRange, OdfCellAddress hostCell, SparklineType type)
    {
        if (dataRange is null)
            throw new ArgumentNullException(nameof(dataRange));

        OdfTableSheetConditionalFormatEngine.AddSparklineGroup(
            MutationContext, dataRange.Value, hostCell, type);
    }


    /// <summary>
    /// Adds a database range to this worksheet.
    /// 新增資料庫範圍至此工作表。
    /// </summary>
    /// <param name="name">The name or identifier. / 資料庫範圍名稱</param>
    /// <param name="range">The cell range. / 目標儲存格範圍</param>
    /// <returns>The result. / 新增的資料庫範圍</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range) =>
        _doc.AddDatabaseRange(name, range);

    /// <summary>
    /// Enables autofilter buttons for the specified range.
    /// 為指定範圍啟用自動篩選按鈕。
    /// </summary>
    /// <param name="range">The cell range. / 要啟用自動篩選的儲存格範圍</param>
    /// <returns>The result. / 對應的資料庫範圍，可繼續設定篩選條件</returns>
    public OdfDatabaseRange AutoFilter(string range)
    {
        if (!OdfCellRange.TryParse(range, out OdfCellRange parsedRange))
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellRange", range));

        return AutoFilter(parsedRange);
    }

    /// <summary>
    /// Enables autofilter buttons for the specified range.
    /// 為指定範圍啟用自動篩選按鈕。
    /// </summary>
    /// <param name="range">The cell range. / 要啟用自動篩選的儲存格範圍</param>
    /// <returns>The result. / 對應的資料庫範圍，可繼續設定篩選條件</returns>
    public OdfDatabaseRange AutoFilter(OdfCellRange range)
    {
        OdfDatabaseRange databaseRange = AddDatabaseRange(CreateDatabaseRangeName("AutoFilter"), EnsureSheetName(range));
        databaseRange.DisplayFilterButtons = true;
        return databaseRange;
    }

    /// <summary>
    /// Sets sort rules for the specified range.
    /// 為指定範圍設定排序規則。
    /// </summary>
    /// <param name="range">The cell range. / 要排序的儲存格範圍</param>
    /// <param name="rules">The value to use. / 排序規則陣列，包含欄位編號與是否遞增</param>
    /// <returns>The result. / 對應的資料庫範圍</returns>
    public OdfDatabaseRange Sort(string range, params (int fieldNumber, bool ascending)[] rules)
    {
        if (!OdfCellRange.TryParse(range, out OdfCellRange parsedRange))
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellRange", range));

        return Sort(parsedRange, rules);
    }

    /// <summary>
    /// Sets sort rules for the specified range.
    /// 為指定範圍設定排序規則。
    /// </summary>
    /// <param name="range">The cell range. / 要排序的儲存格範圍</param>
    /// <param name="rules">The value to use. / 排序規則陣列，包含欄位編號與是否遞增</param>
    /// <returns>The result. / 對應的資料庫範圍</returns>
    public OdfDatabaseRange Sort(OdfCellRange range, params (int fieldNumber, bool ascending)[] rules)
    {
        OdfDatabaseRange databaseRange = AddDatabaseRange(CreateDatabaseRangeName("Sort"), EnsureSheetName(range));
        databaseRange.SetSort(rules);
        return databaseRange;
    }

    private OdfCellRange EnsureSheetName(OdfCellRange range)
    {
        string? startSheet = range.StartAddress.SheetName ?? Name;
        string? endSheet = range.EndAddress.SheetName ?? startSheet;
        return new OdfCellRange(
            new OdfCellAddress(
                range.StartAddress.Row,
                range.StartAddress.Column,
                startSheet,
                range.StartAddress.IsRowAbsolute,
                range.StartAddress.IsColumnAbsolute,
                range.StartAddress.IsSheetAbsolute),
            new OdfCellAddress(
                range.EndAddress.Row,
                range.EndAddress.Column,
                endSheet,
                range.EndAddress.IsRowAbsolute,
                range.EndAddress.IsColumnAbsolute,
                range.EndAddress.IsSheetAbsolute));
    }

    private string CreateDatabaseRangeName(string purpose)
    {
        int index = _doc.GetDatabaseRanges().Count + 1;
        string name;
        do
        {
            name = $"{Name}_{purpose}_{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
        }
        while (DatabaseRangeNameExists(name));

        return name;
    }

    private bool DatabaseRangeNameExists(string name)
    {
        foreach (OdfDatabaseRangeInfo info in _doc.GetDatabaseRanges())
        {
            if (string.Equals(info.Name, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    #endregion
}

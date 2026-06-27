using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region ConditionalFormats

    /// <summary>
    /// 取得此工作表中的 LibreOffice calcext 條件格式規則清單。
    /// </summary>
    public IReadOnlyList<OdfConditionalFormatInfo> ConditionalFormats =>
        OdfTableSheetConditionalFormatEngine.GetConditionalFormats(MutationContext);

    /// <summary>
    /// 取得此工作表中的 LibreOffice calcext 走勢圖群組清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineGroupInfo> SparklineGroups =>
        OdfTableSheetConditionalFormatEngine.GetSparklineGroups(MutationContext);

    /// <summary>
    /// 新增條件格式。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <param name="conditionValue">條件運算式</param>
    /// <param name="styleName">要套用的格式樣式名稱</param>
    public void AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName) =>
        OdfTableSheetConditionalFormatEngine.AddConditionalFormat(
            MutationContext, range, conditionValue, styleName);

    /// <summary>
    /// 新增色階條件格式（兩色或三色）。
    /// </summary>
    /// <param name="range">套用範圍</param>
    /// <param name="minColor">最小值對應色彩</param>
    /// <param name="maxColor">最大值對應色彩</param>
    /// <param name="midColor">中間值對應色彩（可選，設定時為三色色階）</param>
    public void AddColorScaleFormat(OdfCellRange range,
        OdfColor minColor, OdfColor maxColor, OdfColor? midColor = null) =>
        OdfTableSheetConditionalFormatEngine.AddColorScaleFormat(
            MutationContext, range, minColor, maxColor, midColor);

    /// <summary>
    /// 新增資料橫條條件格式。
    /// </summary>
    /// <param name="range">套用範圍</param>
    /// <param name="positiveColor">正值橫條色彩</param>
    /// <param name="negativeColor">負值橫條色彩（可選）</param>
    public void AddDataBarFormat(OdfCellRange range,
        OdfColor positiveColor, OdfColor? negativeColor = null) =>
        OdfTableSheetConditionalFormatEngine.AddDataBarFormat(
            MutationContext, range, positiveColor, negativeColor);

    /// <summary>
    /// 新增資料橫條條件格式。
    /// </summary>
    /// <param name="range">套用範圍</param>
    /// <param name="color">正值橫條色彩</param>
    /// <param name="negativeColor">負值橫條色彩（可選）</param>
    public void AddDataBar(OdfCellRange range, OdfColor color, OdfColor? negativeColor = null) =>
        AddDataBarFormat(range, color, negativeColor);

    /// <summary>
    /// 新增圖示集條件格式。
    /// </summary>
    /// <param name="range">套用範圍</param>
    /// <param name="iconSet">圖示集類型</param>
    public void AddIconSetFormat(OdfCellRange range, OdfIconSetType iconSet) =>
        OdfTableSheetConditionalFormatEngine.AddIconSetFormat(MutationContext, range, iconSet);

    /// <summary>
    /// 新增圖示集條件格式。
    /// </summary>
    /// <param name="range">套用範圍</param>
    /// <param name="iconSet">圖示集類型</param>
    public void AddIconSet(OdfCellRange range, OdfIconSetType iconSet) =>
        AddIconSetFormat(range, iconSet);

    /// <summary>
    /// 在工作表中新增 LibreOffice calcext 走勢圖群組。
    /// </summary>
    /// <param name="dataRange">走勢圖資料來源範圍</param>
    /// <param name="hostCell">顯示走勢圖的儲存格位址</param>
    /// <param name="type">走勢圖類型，預設為折線</param>
    /// <exception cref="ArgumentNullException">當 dataRange 為 null 時拋出</exception>
    public void AddSparklineGroup(OdfCellRange? dataRange, OdfCellAddress hostCell, SparklineType type = SparklineType.Line)
    {
        if (dataRange is null)
            throw new ArgumentNullException(nameof(dataRange));

        OdfTableSheetConditionalFormatEngine.AddSparklineGroup(
            MutationContext, dataRange.Value, hostCell, type);
    }

    /// <summary>
    /// 新增資料庫範圍至此工作表。
    /// </summary>
    /// <param name="name">資料庫範圍名稱</param>
    /// <param name="range">目標儲存格範圍</param>
    /// <returns>新增的資料庫範圍</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range) =>
        _doc.AddDatabaseRange(name, range);

    /// <summary>
    /// 為指定範圍啟用自動篩選按鈕。
    /// </summary>
    /// <param name="range">要啟用自動篩選的儲存格範圍</param>
    /// <returns>對應的資料庫範圍，可繼續設定篩選條件</returns>
    public OdfDatabaseRange AutoFilter(string range)
    {
        if (!OdfCellRange.TryParse(range, out OdfCellRange parsedRange))
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellRange", range));

        return AutoFilter(parsedRange);
    }

    /// <summary>
    /// 為指定範圍啟用自動篩選按鈕。
    /// </summary>
    /// <param name="range">要啟用自動篩選的儲存格範圍</param>
    /// <returns>對應的資料庫範圍，可繼續設定篩選條件</returns>
    public OdfDatabaseRange AutoFilter(OdfCellRange range)
    {
        OdfDatabaseRange databaseRange = AddDatabaseRange(CreateDatabaseRangeName("AutoFilter"), EnsureSheetName(range));
        databaseRange.DisplayFilterButtons = true;
        return databaseRange;
    }

    /// <summary>
    /// 為指定範圍設定排序規則。
    /// </summary>
    /// <param name="range">要排序的儲存格範圍</param>
    /// <param name="rules">排序規則陣列，包含欄位編號與是否遞增</param>
    /// <returns>對應的資料庫範圍</returns>
    public OdfDatabaseRange Sort(string range, params (int fieldNumber, bool ascending)[] rules)
    {
        if (!OdfCellRange.TryParse(range, out OdfCellRange parsedRange))
            throw new FormatException(OdfLocalizer.GetMessage("Err_OdfTableSheet_InvalidCellRange", range));

        return Sort(parsedRange, rules);
    }

    /// <summary>
    /// 為指定範圍設定排序規則。
    /// </summary>
    /// <param name="range">要排序的儲存格範圍</param>
    /// <param name="rules">排序規則陣列，包含欄位編號與是否遞增</param>
    /// <returns>對應的資料庫範圍</returns>
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

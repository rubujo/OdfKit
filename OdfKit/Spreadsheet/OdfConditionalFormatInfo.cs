using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示工作表中一筆 LibreOffice calcext 條件格式規則的摘要資訊。
/// </summary>
/// <param name="kind">條件格式種類</param>
/// <param name="targetRangeAddress">ODF 目標範圍位址字串（<c>calcext:target-range-address</c>）</param>
/// <param name="conditionValue">條件運算式（僅 <see cref="OdfConditionalFormatKind.Condition"/> 時有值）</param>
/// <param name="styleName">要套用的樣式名稱（僅 <see cref="OdfConditionalFormatKind.Condition"/> 時有值）</param>
/// <param name="minColor">色階最小值色彩（僅 <see cref="OdfConditionalFormatKind.ColorScale"/> 時有值）</param>
/// <param name="maxColor">色階最大值色彩（僅 <see cref="OdfConditionalFormatKind.ColorScale"/> 時有值）</param>
/// <param name="midColor">色階中間值色彩（三色色階時有值）</param>
/// <param name="positiveColor">資料橫條正值色彩（僅 <see cref="OdfConditionalFormatKind.DataBar"/> 時有值）</param>
/// <param name="negativeColor">資料橫條負值色彩（可選）</param>
/// <param name="iconSetTypeName">圖示集類型名稱（<c>calcext:icon-set-type</c> 原文）</param>
/// <param name="iconSetType">已辨識的圖示集類型（無法對應時為 null）</param>
public sealed class OdfConditionalFormatInfo(
    OdfConditionalFormatKind kind,
    string targetRangeAddress,
    string? conditionValue = null,
    string? styleName = null,
    OdfColor? minColor = null,
    OdfColor? maxColor = null,
    OdfColor? midColor = null,
    OdfColor? positiveColor = null,
    OdfColor? negativeColor = null,
    string? iconSetTypeName = null,
    OdfIconSetType? iconSetType = null)
{
    /// <summary>
    /// 取得條件格式種類。
    /// </summary>
    public OdfConditionalFormatKind Kind { get; } = kind;

    /// <summary>
    /// 取得 ODF 目標範圍位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得條件運算式。
    /// </summary>
    public string? ConditionValue { get; } = conditionValue;

    /// <summary>
    /// 取得要套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;

    /// <summary>
    /// 取得色階最小值色彩。
    /// </summary>
    public OdfColor? MinColor { get; } = minColor;

    /// <summary>
    /// 取得色階最大值色彩。
    /// </summary>
    public OdfColor? MaxColor { get; } = maxColor;

    /// <summary>
    /// 取得色階中間值色彩。
    /// </summary>
    public OdfColor? MidColor { get; } = midColor;

    /// <summary>
    /// 取得資料橫條正值色彩。
    /// </summary>
    public OdfColor? PositiveColor { get; } = positiveColor;

    /// <summary>
    /// 取得資料橫條負值色彩。
    /// </summary>
    public OdfColor? NegativeColor { get; } = negativeColor;

    /// <summary>
    /// 取得圖示集類型名稱原文。
    /// </summary>
    public string? IconSetTypeName { get; } = iconSetTypeName;

    /// <summary>
    /// 取得已辨識的圖示集類型。
    /// </summary>
    public OdfIconSetType? IconSetType { get; } = iconSetType;

    /// <summary>
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">解析成功時傳回的儲存格範圍</param>
    /// <returns>若解析成功則為 true，否則為 false</returns>
    public bool TryGetTargetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(TargetRangeAddress, out range);
}

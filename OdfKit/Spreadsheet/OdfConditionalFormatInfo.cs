using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents summary information for a LibreOffice calcext conditional formatting rule in a worksheet.
/// 表示工作表中一筆 LibreOffice calcext 條件格式規則的摘要資訊。
/// </summary>
/// <param name="kind">The conditional formatting kind. / 條件格式種類。</param>
/// <param name="targetRangeAddress">The ODF target range address string from <c>calcext:target-range-address</c>. / ODF 目標範圍位址字串（<c>calcext:target-range-address</c>）。</param>
/// <param name="conditionValue">The condition expression, available only for <see cref="OdfConditionalFormatKind.Condition"/>. / 條件運算式，僅 <see cref="OdfConditionalFormatKind.Condition"/> 時有值。</param>
/// <param name="styleName">The style name to apply, available only for <see cref="OdfConditionalFormatKind.Condition"/>. / 要套用的樣式名稱，僅 <see cref="OdfConditionalFormatKind.Condition"/> 時有值。</param>
/// <param name="minColor">The color scale minimum color, available only for <see cref="OdfConditionalFormatKind.ColorScale"/>. / 色階最小值色彩，僅 <see cref="OdfConditionalFormatKind.ColorScale"/> 時有值。</param>
/// <param name="maxColor">The color scale maximum color, available only for <see cref="OdfConditionalFormatKind.ColorScale"/>. / 色階最大值色彩，僅 <see cref="OdfConditionalFormatKind.ColorScale"/> 時有值。</param>
/// <param name="midColor">The color scale midpoint color, available for three-color scales. / 色階中間值色彩，三色色階時有值。</param>
/// <param name="positiveColor">The data bar positive value color, available only for <see cref="OdfConditionalFormatKind.DataBar"/>. / 資料橫條正值色彩，僅 <see cref="OdfConditionalFormatKind.DataBar"/> 時有值。</param>
/// <param name="negativeColor">The optional data bar negative value color. / 資料橫條負值色彩，可選。</param>
/// <param name="iconSetTypeName">The original icon set type name from <c>calcext:icon-set-type</c>. / 圖示集類型名稱（<c>calcext:icon-set-type</c> 原文）。</param>
/// <param name="iconSetType">The recognized icon set type, or <see langword="null"/> when it cannot be mapped. / 已辨識的圖示集類型，無法對應時為 <see langword="null"/>。</param>
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
    /// Gets the conditional formatting kind.
    /// 取得條件格式種類。
    /// </summary>
    public OdfConditionalFormatKind Kind { get; } = kind;

    /// <summary>
    /// Gets the ODF target range address string.
    /// 取得 ODF 目標範圍位址字串。
    /// </summary>
    public string TargetRangeAddress { get; } = targetRangeAddress ?? string.Empty;

    /// <summary>
    /// Gets the condition expression.
    /// 取得條件運算式。
    /// </summary>
    public string? ConditionValue { get; } = conditionValue;

    /// <summary>
    /// Gets the style name to apply.
    /// 取得要套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;

    /// <summary>
    /// Gets the color scale minimum color.
    /// 取得色階最小值色彩。
    /// </summary>
    public OdfColor? MinColor { get; } = minColor;

    /// <summary>
    /// Gets the color scale maximum color.
    /// 取得色階最大值色彩。
    /// </summary>
    public OdfColor? MaxColor { get; } = maxColor;

    /// <summary>
    /// Gets the color scale midpoint color.
    /// 取得色階中間值色彩。
    /// </summary>
    public OdfColor? MidColor { get; } = midColor;

    /// <summary>
    /// Gets the data bar positive value color.
    /// 取得資料橫條正值色彩。
    /// </summary>
    public OdfColor? PositiveColor { get; } = positiveColor;

    /// <summary>
    /// Gets the data bar negative value color.
    /// 取得資料橫條負值色彩。
    /// </summary>
    public OdfColor? NegativeColor { get; } = negativeColor;

    /// <summary>
    /// Gets the original icon set type name.
    /// 取得圖示集類型名稱原文。
    /// </summary>
    public string? IconSetTypeName { get; } = iconSetTypeName;

    /// <summary>
    /// Gets the recognized icon set type.
    /// 取得已辨識的圖示集類型。
    /// </summary>
    public OdfIconSetType? IconSetType { get; } = iconSetType;

    /// <summary>
    /// Attempts to parse <see cref="TargetRangeAddress"/> as an <see cref="OdfCellRange"/>.
    /// 嘗試將 <see cref="TargetRangeAddress"/> 解析為 <see cref="OdfCellRange"/>。
    /// </summary>
    /// <param name="range">The cell range returned when parsing succeeds. / 解析成功時傳回的儲存格範圍。</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>. / 若解析成功則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool TryGetTargetRange(out OdfCellRange range) =>
        OdfCellRange.TryParse(TargetRangeAddress, out range);
}

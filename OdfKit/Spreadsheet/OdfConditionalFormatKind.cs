namespace OdfKit.Spreadsheet;

/// <summary>
/// LibreOffice calcext 條件格式的種類。
/// </summary>
public enum OdfConditionalFormatKind
{
    /// <summary>
    /// 單一條件與樣式對應（<c>calcext:condition</c>）
    /// </summary>
    Condition,

    /// <summary>
    /// 色階條件格式（<c>calcext:color-scale</c>）
    /// </summary>
    ColorScale,

    /// <summary>
    /// 資料橫條條件格式（<c>calcext:data-bar</c>）
    /// </summary>
    DataBar,

    /// <summary>
    /// 圖示集條件格式（<c>calcext:icon-set</c>）
    /// </summary>
    IconSet,
}

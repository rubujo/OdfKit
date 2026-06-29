namespace OdfKit.Spreadsheet;

/// <summary>
/// Defines LibreOffice calcext sparkline types.
/// 定義 LibreOffice calcext 走勢圖的類型。
/// </summary>
public enum SparklineType
{
    /// <summary>
    /// A line sparkline.
    /// 折線走勢圖。
    /// </summary>
    Line,

    /// <summary>
    /// A column sparkline.
    /// 直條走勢圖。
    /// </summary>
    Column,

    /// <summary>
    /// A win/loss sparkline with positive and negative bars.
    /// 盈虧走勢圖（正負條）。
    /// </summary>
    WinLoss
}

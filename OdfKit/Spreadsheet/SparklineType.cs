namespace OdfKit.Spreadsheet;

/// <summary>
/// 定義 LibreOffice calcext 走勢圖的類型。
/// </summary>
public enum SparklineType
{
    /// <summary>折線走勢圖。</summary>
    Line,

    /// <summary>直條走勢圖。</summary>
    Column,

    /// <summary>盈虧走勢圖（正負條）。</summary>
    WinLoss
}

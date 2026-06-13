namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF <c>style:family</c> 屬性的官方樣式家族 token。
/// </summary>
public enum OdfStyleFamily
{
    /// <summary>
    /// 文字樣式家族。
    /// </summary>
    Text,

    /// <summary>
    /// 段落樣式家族。
    /// </summary>
    Paragraph,

    /// <summary>
    /// 區段樣式家族。
    /// </summary>
    Section,

    /// <summary>
    /// Ruby 標註樣式家族。
    /// </summary>
    Ruby,

    /// <summary>
    /// 表格樣式家族。
    /// </summary>
    Table,

    /// <summary>
    /// 表格欄樣式家族。
    /// </summary>
    TableColumn,

    /// <summary>
    /// 表格列樣式家族。
    /// </summary>
    TableRow,

    /// <summary>
    /// 表格儲存格樣式家族。
    /// </summary>
    TableCell,

    /// <summary>
    /// 圖形樣式家族。
    /// </summary>
    Graphic,

    /// <summary>
    /// 簡報樣式家族。
    /// </summary>
    Presentation,

    /// <summary>
    /// 繪圖頁樣式家族。
    /// </summary>
    DrawingPage,

    /// <summary>
    /// 圖表樣式家族。
    /// </summary>
    Chart
}

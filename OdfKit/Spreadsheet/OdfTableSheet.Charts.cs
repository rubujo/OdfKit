using OdfKit.Chart;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 圖表

    /// <summary>
    /// Inserts a chart bound to cell range data into this worksheet.
    /// 在此工作表中插入一個與儲存格範圍資料繫結的圖表。
    /// </summary>
    /// <param name="dataRange">The cell range bound to the data. / 資料繫結的儲存格範圍。</param>
    /// <param name="chartType">The chart type, defaulting to a bar chart. / 圖表類型，預設為條形圖。</param>
    /// <param name="x">The chart frame's left margin, defaulting to 1cm. / 圖表框左邊距，預設 1cm。</param>
    /// <param name="y">The chart frame's top margin, defaulting to 1cm. / 圖表框上邊距，預設 1cm。</param>
    /// <param name="width">The chart frame's width, defaulting to 12cm. / 圖表框寬度，預設 12cm。</param>
    /// <param name="height">The chart frame's height, defaulting to 7cm. / 圖表框高度，預設 7cm。</param>
    /// <param name="firstRowAsHeader">Whether the first data row is treated as the series header, defaulting to true. / 資料首列作為序列標題，預設 true。</param>
    /// <param name="firstColumnAsLabel">Whether the first data column is treated as the X-axis category label, defaulting to true. / 資料首欄作為 X 軸分類標籤，預設 true。</param>
    /// <returns>
    /// 可進一步設定的 <see cref="OdfChartDocument"/>。
    /// 呼叫端修改後可直接儲存父文件，父文件會自動 flush 已追蹤的嵌入圖表；
    /// 也可手動呼叫 <c>Save()</c> 以提早寫回封裝。
    /// 請勿對此物件呼叫 <c>Dispose()</c>（生命週期由父文件管理）。
    /// </returns>
    public OdfChartDocument InsertChart(
        OdfCellRange dataRange,
        OdfChartType chartType = OdfChartType.Bar,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null,
        bool firstRowAsHeader = true,
        bool firstColumnAsLabel = true) =>
        OdfTableSheetChartEngine.InsertChart(
            MutationContext,
            dataRange,
            chartType,
            x,
            y,
            width,
            height,
            firstRowAsHeader,
            firstColumnAsLabel);

    #endregion
}

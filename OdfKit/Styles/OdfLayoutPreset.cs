namespace OdfKit.Styles;

/// <summary>
/// Provides the OdfLayoutPreset API.
/// 表示高階 builder 可共用的文件版面 preset。
/// </summary>
public sealed class OdfLayoutPreset
{
    /// <summary>
    /// Performs business deck.
    /// 取得適合年度報告與商業簡報的內建版面。
    /// </summary>
    public static OdfLayoutPreset BusinessDeck => new()
    {
        Name = "BusinessDeck",
        TitleBounds = new OdfLayoutBounds(1, 0.8, 18, 1.5),
        SubtitleBounds = new OdfLayoutBounds(1.5, 4, 16, 1.5),
        LeftColumnBounds = new OdfLayoutBounds(1, 3, 8, 9),
        RightColumnBounds = new OdfLayoutBounds(10, 3, 8, 9),
        ChartBounds = new OdfLayoutBounds(2, 3, 15, 8),
    };

    /// <summary>
    /// Performs flow diagram.
    /// 取得適合流程圖與架構圖的內建版面。
    /// </summary>
    public static OdfLayoutPreset FlowDiagram => new()
    {
        Name = "FlowDiagram",
        FlowNodeBounds = new OdfLayoutBounds(1, 1, 4, 1.4),
        FlowNodeGapCm = 2,
    };

    /// <summary>
    /// Gets the Name value.
    /// 取得或設定版面 preset 名稱。
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Gets the TitleBounds value.
    /// 取得或設定標題區塊。
    /// </summary>
    public OdfLayoutBounds TitleBounds { get; set; } = new(1, 1, 16, 2);

    /// <summary>
    /// Gets the SubtitleBounds value.
    /// 取得或設定副標題區塊。
    /// </summary>
    public OdfLayoutBounds SubtitleBounds { get; set; } = new(1.5, 4, 16, 1.5);

    /// <summary>
    /// Gets the LeftColumnBounds value.
    /// 取得或設定左欄內容區塊。
    /// </summary>
    public OdfLayoutBounds LeftColumnBounds { get; set; } = new(1, 3, 8, 9);

    /// <summary>
    /// Gets the RightColumnBounds value.
    /// 取得或設定右欄內容區塊。
    /// </summary>
    public OdfLayoutBounds RightColumnBounds { get; set; } = new(10, 3, 8, 9);

    /// <summary>
    /// Gets the ChartBounds value.
    /// 取得或設定圖表區塊。
    /// </summary>
    public OdfLayoutBounds ChartBounds { get; set; } = new(2, 3, 15, 8);

    /// <summary>
    /// Gets the FlowNodeBounds value.
    /// 取得或設定第一個流程圖節點區塊。
    /// </summary>
    public OdfLayoutBounds FlowNodeBounds { get; set; } = new(1, 1, 4, 1.4);

    /// <summary>
    /// Gets the FlowNodeGapCm value.
    /// 取得或設定流程圖節點之間的水平間距（公分）。
    /// </summary>
    public double FlowNodeGapCm { get; set; } = 2;

    /// <summary>
    /// Gets flow node bounds.
    /// 依序號取得流程圖節點區塊。
    /// </summary>
    /// <param name="index">流程圖節點序號</param>
    /// <returns>對應的節點區塊</returns>
    public OdfLayoutBounds GetFlowNodeBounds(int index)
    {
        int normalized = Math.Max(0, index);
        double x = FlowNodeBounds.XCm + normalized * (FlowNodeBounds.WidthCm + FlowNodeGapCm);
        return new OdfLayoutBounds(x, FlowNodeBounds.YCm, FlowNodeBounds.WidthCm, FlowNodeBounds.HeightCm);
    }
}

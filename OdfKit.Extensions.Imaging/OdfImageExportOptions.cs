namespace OdfKit.Export;

/// <summary>
/// 試算表影像匯出的選項設定。
/// </summary>
public sealed class OdfImageExportOptions
{
    /// <summary>取得或設定渲染的欄數，預設為 10。</summary>
    public int ColumnCount { get; init; } = 10;

    /// <summary>取得或設定渲染的列數，預設為 20。</summary>
    public int RowCount { get; init; } = 20;

    /// <summary>取得或設定每個儲存格的像素寬度，預設為 80。</summary>
    public int CellWidthPx { get; init; } = 80;

    /// <summary>取得或設定每個儲存格的像素高度，預設為 24。</summary>
    public int CellHeightPx { get; init; } = 24;

    /// <summary>取得或設定文字字型大小，預設為 13 像素。</summary>
    public float FontSizePx { get; init; } = 13f;
}

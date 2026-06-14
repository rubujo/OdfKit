namespace OdfKit.Export;

/// <summary>
/// HTML 匯出的選項設定。
/// </summary>
public sealed class OdfHtmlExportOptions
{
    /// <summary>
    /// 取得或設定是否輸出完整 HTML 頁面（含 html / head / body 標籤），預設為 true。
    /// 設為 false 時僅輸出 body 內容片段。
    /// </summary>
    public bool FullPage { get; init; } = true;

    /// <summary>
    /// 取得或設定頁面標題，用於 title 標籤，預設為空字串。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定是否內嵌基本 CSS 樣式表，預設為 true。
    /// </summary>
    public bool InlineStyles { get; init; } = true;

    /// <summary>
    /// 取得或設定字元編碼宣告，預設為 utf-8。
    /// </summary>
    public string Charset { get; init; } = "utf-8";
}

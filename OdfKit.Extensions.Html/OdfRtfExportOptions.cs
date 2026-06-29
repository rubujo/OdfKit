namespace OdfKit.Export;

/// <summary>
/// RTF 匯出的選項設定。
/// </summary>
public sealed class OdfRtfExportOptions
{
    /// <summary>
    /// Gets or sets default font name.
    /// 取得或設定預設字型名稱。
    /// </summary>
    public string DefaultFontName { get; init; } = "Arial";
}

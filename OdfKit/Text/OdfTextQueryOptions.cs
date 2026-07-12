namespace OdfKit.Text;

/// <summary>
/// Configures task-oriented text searches.
/// 設定任務導向文字搜尋。
/// </summary>
public sealed class OdfTextQueryOptions
{
    /// <summary>
    /// Gets the default text query options.
    /// 取得預設文字查詢選項。
    /// </summary>
    public static OdfTextQueryOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether matching is case-sensitive.
    /// 取得或設定比對是否區分大小寫。
    /// </summary>
    public bool MatchCase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether matches must be bounded by non-word characters.
    /// 取得或設定符合項目是否必須以非單字字元為邊界。
    /// </summary>
    public bool WholeWord { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of matches to return.
    /// 取得或設定要回傳的符合項目數量上限。
    /// </summary>
    public int MaxResults { get; set; } = int.MaxValue;
}

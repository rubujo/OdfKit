namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf mail merge options.
/// 提供郵件合併（Mail Merge）作業的進階設定選項。
/// </summary>
public sealed class OdfMailMergeOptions
{
    /// <summary>
    /// Gets or sets region start token.
    /// 取得或設定用以標記區段開始的語法。預設為 <c>{{TableStart:</c>。
    /// </summary>
    public string RegionStartToken { get; init; } = "{{TableStart:";

    /// <summary>
    /// Gets or sets region end token.
    /// 取得或設定用以標記區段結束的語法。預設為 <c>{{TableEnd:</c>。
    /// </summary>
    public string RegionEndToken { get; init; } = "{{TableEnd:";

    /// <summary>
    /// Gets or sets max nesting depth.
    /// 取得或設定最大巢狀解析深度限制，用以防止因遞迴參照造成無窮迴圈。預設為 8。
    /// </summary>
    public int MaxNestingDepth { get; init; } = 8;
}

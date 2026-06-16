namespace OdfKit.Text;

/// <summary>
/// 提供郵件合併（Mail Merge）作業的進階設定選項。
/// </summary>
public sealed class OdfMailMergeOptions
{
    /// <summary>
    /// 取得或設定用以標記區段開始的語法。預設為 <c>{{TableStart:</c>。
    /// </summary>
    public string RegionStartToken { get; init; } = "{{TableStart:";

    /// <summary>
    /// 取得或設定用以標記區段結束的語法。預設為 <c>{{TableEnd:</c>。
    /// </summary>
    public string RegionEndToken { get; init; } = "{{TableEnd:";

    /// <summary>
    /// 取得或設定最大巢狀解析深度限制，用以防止因遞迴參照造成無窮迴圈。預設為 8。
    /// </summary>
    public int MaxNestingDepth { get; init; } = 8;
}

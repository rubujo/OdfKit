namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片上一筆動畫效果的摘要資訊。
/// </summary>
/// <param name="kind">動畫種類。</param>
/// <param name="targetElementId">目標圖形識別碼。</param>
/// <param name="effect">動畫效果類型。</param>
/// <param name="trigger">動畫觸發方式。</param>
/// <param name="presetId">LibreOffice 預設動畫識別碼原文。</param>
public sealed class OdfAnimationInfo(
    OdfAnimationKind kind,
    string targetElementId,
    OdfAnimationEffect effect,
    OdfAnimationTrigger trigger,
    string? presetId)
{
    /// <summary>
    /// 取得動畫種類。
    /// </summary>
    public OdfAnimationKind Kind { get; } = kind;

    /// <summary>
    /// 取得目標圖形識別碼。
    /// </summary>
    public string TargetElementId { get; } = targetElementId ?? string.Empty;

    /// <summary>
    /// 取得動畫效果類型。
    /// </summary>
    public OdfAnimationEffect Effect { get; } = effect;

    /// <summary>
    /// 取得動畫觸發方式。
    /// </summary>
    public OdfAnimationTrigger Trigger { get; } = trigger;

    /// <summary>
    /// 取得 LibreOffice 預設動畫識別碼原文。
    /// </summary>
    public string? PresetId { get; } = presetId;
}

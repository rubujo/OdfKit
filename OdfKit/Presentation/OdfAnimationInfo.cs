using System;
using System.Globalization;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片上一筆動畫效果的摘要資訊。
/// </summary>
/// <param name="kind">動畫種類。</param>
/// <param name="targetElementId">目標圖形識別碼。</param>
/// <param name="effect">動畫效果類型。</param>
/// <param name="trigger">動畫觸發方式。</param>
/// <param name="presetId">LibreOffice 預設動畫識別碼原文。</param>
/// <param name="duration">持續時間原文（<c>smil:dur</c>）。</param>
/// <param name="begin">開始時間原文（<c>smil:begin</c>）。</param>
/// <param name="sequenceIndex">在投影片動畫序列中的順序索引（以 0 為基準）。</param>
public sealed class OdfAnimationInfo(
    OdfAnimationKind kind,
    string targetElementId,
    OdfAnimationEffect effect,
    OdfAnimationTrigger trigger,
    string? presetId,
    string? duration = null,
    string? begin = null,
    int sequenceIndex = 0)
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

    /// <summary>
    /// 取得持續時間原文。
    /// </summary>
    public string? Duration { get; } = duration;

    /// <summary>
    /// 取得開始時間原文。
    /// </summary>
    public string? Begin { get; } = begin;

    /// <summary>
    /// 取得在投影片動畫序列中的順序索引。
    /// </summary>
    public int SequenceIndex { get; } = sequenceIndex;

    /// <summary>
    /// 嘗試將 <see cref="Duration"/> 解析為秒數。
    /// </summary>
    /// <param name="seconds">解析成功時傳回的秒數。</param>
    /// <returns>若可解析則為 <see langword="true"/>。</returns>
    public bool TryGetDurationSeconds(out double seconds) =>
        TryParseSmilSeconds(Duration, out seconds);

    /// <summary>
    /// 嘗試將 <see cref="Begin"/> 中的延遲偏移解析為秒數（僅支援純數值秒或 <c>prev.end+Ns</c> 形式）。
    /// </summary>
    /// <param name="seconds">解析成功時傳回的延遲秒數。</param>
    /// <returns>若可解析則為 <see langword="true"/>。</returns>
    public bool TryGetDelaySeconds(out double seconds) =>
        TryParseSmilDelay(Begin, out seconds);

    private static bool TryParseSmilSeconds(string? value, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrEmpty(value))
            return false;

        string trimmed = value!.Trim();
        if (trimmed.EndsWith("s", StringComparison.Ordinal) &&
            double.TryParse(trimmed.Substring(0, trimmed.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return true;

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    private static bool TryParseSmilDelay(string? value, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrEmpty(value))
            return true;

        string trimmed = value!.Trim();
        int plusIndex = trimmed.LastIndexOf('+');
        if (plusIndex >= 0)
            return TryParseSmilSeconds(trimmed.Substring(plusIndex + 1), out seconds);

        if (trimmed.StartsWith("prev.end", StringComparison.Ordinal))
            return true;

        return TryParseSmilSeconds(trimmed, out seconds);
    }
}

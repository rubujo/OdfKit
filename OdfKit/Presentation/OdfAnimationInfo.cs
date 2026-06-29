using System;
using System.Globalization;

namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for an animation effect on a slide.
/// 表示投影片上一筆動畫效果的摘要資訊。
/// </summary>
/// <param name="kind">The animation kind. / 動畫種類。</param>
/// <param name="targetElementId">The target shape identifier. / 目標圖形識別碼。</param>
/// <param name="effect">The animation effect type. / 動畫效果類型。</param>
/// <param name="trigger">The animation trigger mode. / 動畫觸發方式。</param>
/// <param name="presetId">The raw LibreOffice preset animation identifier. / LibreOffice 預設動畫識別碼原文。</param>
/// <param name="duration">The raw duration value (<c>smil:dur</c>). / 持續時間原文（<c>smil:dur</c>）。</param>
/// <param name="begin">The raw begin value (<c>smil:begin</c>). / 開始時間原文（<c>smil:begin</c>）。</param>
/// <param name="sequenceIndex">The zero-based order index in the slide animation sequence. / 在投影片動畫序列中的順序索引（以 0 為基準）。</param>
/// <param name="paragraphStartIndex">The zero-based starting paragraph index for PPTX paragraph animation. / PPTX 逐段落動畫的起始段落索引（以 0 為基準）。</param>
/// <param name="paragraphEndIndex">The zero-based ending paragraph index for PPTX paragraph animation. / PPTX 逐段落動畫的結束段落索引（以 0 為基準）。</param>
public sealed class OdfAnimationInfo(
    OdfAnimationKind kind,
    string targetElementId,
    OdfAnimationEffect effect,
    OdfAnimationTrigger trigger,
    string? presetId,
    string? duration = null,
    string? begin = null,
    int sequenceIndex = 0,
    int? paragraphStartIndex = null,
    int? paragraphEndIndex = null)
{
    /// <summary>
    /// Gets the animation kind.
    /// 取得動畫種類。
    /// </summary>
    public OdfAnimationKind Kind { get; } = kind;

    /// <summary>
    /// Gets the target shape identifier.
    /// 取得目標圖形識別碼。
    /// </summary>
    public string TargetElementId { get; } = targetElementId ?? string.Empty;

    /// <summary>
    /// Gets the animation effect type.
    /// 取得動畫效果類型。
    /// </summary>
    public OdfAnimationEffect Effect { get; } = effect;

    /// <summary>
    /// Gets the animation trigger mode.
    /// 取得動畫觸發方式。
    /// </summary>
    public OdfAnimationTrigger Trigger { get; } = trigger;

    /// <summary>
    /// Gets the raw LibreOffice preset animation identifier.
    /// 取得 LibreOffice 預設動畫識別碼原文。
    /// </summary>
    public string? PresetId { get; } = presetId;

    /// <summary>
    /// Gets the raw duration value.
    /// 取得持續時間原文。
    /// </summary>
    public string? Duration { get; } = duration;

    /// <summary>
    /// Gets the raw begin value.
    /// 取得開始時間原文。
    /// </summary>
    public string? Begin { get; } = begin;

    /// <summary>
    /// Gets the order index in the slide animation sequence.
    /// 取得在投影片動畫序列中的順序索引。
    /// </summary>
    public int SequenceIndex { get; } = sequenceIndex;

    /// <summary>
    /// Gets the zero-based starting paragraph index for PPTX paragraph animation.
    /// 取得 PPTX 逐段落動畫的起始段落索引（以 0 為基準）。
    /// </summary>
    public int? ParagraphStartIndex { get; } = paragraphStartIndex;

    /// <summary>
    /// Gets the zero-based ending paragraph index for PPTX paragraph animation.
    /// 取得 PPTX 逐段落動畫的結束段落索引（以 0 為基準）。
    /// </summary>
    public int? ParagraphEndIndex { get; } = paragraphEndIndex;

    /// <summary>
    /// Attempts to parse <see cref="Duration"/> as seconds.
    /// 嘗試將 <see cref="Duration"/> 解析為秒數。
    /// </summary>
    /// <param name="seconds">The parsed seconds when successful. / 解析成功時傳回的秒數。</param>
    /// <returns><see langword="true"/> when parsing succeeds. / 若可解析則為 <see langword="true"/>。</returns>
    public bool TryGetDurationSeconds(out double seconds) =>
        TryParseSmilSeconds(Duration, out seconds);

    /// <summary>
    /// Attempts to parse the delay offset in <see cref="Begin"/> as seconds.
    /// 嘗試將 <see cref="Begin"/> 中的延遲偏移解析為秒數。
    /// </summary>
    /// <remarks>
    /// Only numeric seconds or <c>prev.end+Ns</c> forms are supported.
    /// 僅支援純數值秒或 <c>prev.end+Ns</c> 形式。
    /// </remarks>
    /// <param name="seconds">The parsed delay seconds when successful. / 解析成功時傳回的延遲秒數。</param>
    /// <returns><see langword="true"/> when parsing succeeds. / 若可解析則為 <see langword="true"/>。</returns>
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

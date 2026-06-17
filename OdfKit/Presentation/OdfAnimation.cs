using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示高階簡報動畫效果的類別。
/// </summary>
public sealed partial class OdfAnimation
{
    /// <summary>
    /// 取得底層的 ODF 動畫節點。
    /// </summary>
    public OdfNode Node { get; }

    /// <summary>
    /// 取得目標元素識別碼。
    /// </summary>
    public string TargetElementId { get; }

    /// <summary>
    /// 取得動畫效果類型。
    /// </summary>
    public OdfAnimationEffect Effect { get; }

    /// <summary>
    /// 取得動畫觸發方式。
    /// </summary>
    public OdfAnimationTrigger Trigger { get; }

    /// <summary>
    /// 初始化 <see cref="OdfAnimation"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="targetElementId">目標元素識別碼。</param>
    /// <param name="effect">動畫效果類型。</param>
    /// <param name="trigger">動畫觸發方式。</param>
    public OdfAnimation(OdfNode node, string targetElementId, OdfAnimationEffect effect, OdfAnimationTrigger trigger)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TargetElementId = targetElementId ?? throw new ArgumentNullException(nameof(targetElementId));
        Effect = effect;
        Trigger = trigger;
    }
}

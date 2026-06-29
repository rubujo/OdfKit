using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a high-level presentation animation effect.
/// 表示高階簡報動畫效果的類別。
/// </summary>
public sealed partial class OdfAnimation
{
    /// <summary>
    /// Gets the underlying ODF animation node.
    /// 取得底層的 ODF 動畫節點。
    /// </summary>
    public OdfNode Node { get; }

    /// <summary>
    /// Gets the target element identifier.
    /// 取得目標元素識別碼。
    /// </summary>
    public string TargetElementId { get; }

    /// <summary>
    /// Gets the animation effect type.
    /// 取得動畫效果類型。
    /// </summary>
    public OdfAnimationEffect Effect { get; }

    /// <summary>
    /// Gets the animation trigger mode.
    /// 取得動畫觸發方式。
    /// </summary>
    public OdfAnimationTrigger Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfAnimation"/> class.
    /// 初始化 <see cref="OdfAnimation"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
    /// <param name="targetElementId">The target element identifier. / 目標元素識別碼。</param>
    /// <param name="effect">The animation effect type. / 動畫效果類型。</param>
    /// <param name="trigger">The animation trigger mode. / 動畫觸發方式。</param>
    public OdfAnimation(OdfNode node, string targetElementId, OdfAnimationEffect effect, OdfAnimationTrigger trigger)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TargetElementId = targetElementId ?? throw new ArgumentNullException(nameof(targetElementId));
        Effect = effect;
        Trigger = trigger;
    }
}

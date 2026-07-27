using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a high-level presentation animation effect.
/// 表示高階簡報動畫效果的類別。
/// </summary>
public sealed class OdfAnimation
{
    private const string SmilNs = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

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

    /// <summary>
    /// Sets the animation duration and synchronizes child node <c>smil:dur</c> values.
    /// 設定動畫效果的持續時間，並同步更新子節點的 <c>smil:dur</c>。
    /// </summary>
    /// <param name="duration">The duration. / 持續時間。</param>
    /// <returns>The current animation instance. / 目前動畫執行個體。</returns>
    public OdfAnimation SetDuration(TimeSpan duration)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(duration));

        string durStr = OdfSmilTime.FormatDuration(duration);
        Node.SetAttribute("dur", SmilNs, durStr, "smil");
        PropagateSmilDuration(Node, durStr);
        return this;
    }

    /// <summary>
    /// Sets the animation startup delay and updates <c>smil:begin</c> according to the trigger mode.
    /// 設定動畫效果的延遲啟動時間，並依觸發方式更新 <c>smil:begin</c>。
    /// </summary>
    /// <param name="delay">The delay. / 延遲時間。</param>
    /// <returns>The current animation instance. / 目前動畫執行個體。</returns>
    public OdfAnimation SetDelay(TimeSpan delay)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfLessThan(delay, TimeSpan.Zero, nameof(delay));

        string delayStr = OdfSmilTime.FormatDelay(delay);
        string beginVal = Trigger switch
        {
            OdfAnimationTrigger.AfterPrevious => "prev.end" + (delay > TimeSpan.Zero ? $"+{delayStr}" : string.Empty),
            _ => delay > TimeSpan.Zero ? delayStr : "0s",
        };
        Node.SetAttribute("begin", SmilNs, beginVal, "smil");
        return this;
    }

    private static void PropagateSmilDuration(OdfNode node, string durStr)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.GetAttribute("dur", SmilNs) is not null)
            {
                child.SetAttribute("dur", SmilNs, durStr, "smil");
            }

            PropagateSmilDuration(child, durStr);
        }
    }
}

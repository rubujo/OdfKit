using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

public partial class OdfAnimation
{
    private const string SmilNs = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

    /// <summary>
    /// Sets the animation duration and synchronizes child node <c>smil:dur</c> values.
    /// 設定動畫效果的持續時間，並同步更新子節點的 <c>smil:dur</c>。
    /// </summary>
    /// <param name="duration">The duration. / 持續時間。</param>
    /// <returns>The current animation instance. / 目前動畫執行個體。</returns>
    public OdfAnimation SetDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

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
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));

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

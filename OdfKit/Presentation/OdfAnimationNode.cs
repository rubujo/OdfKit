using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents an ODF animation node.
/// 表示 ODF 動畫節點的類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
public class OdfAnimationNode(OdfNode node)
{
    /// <summary>
    /// Gets the underlying ODF node.
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// Gets the animation node type.
    /// 取得動畫節點的型態。
    /// </summary>
    public OdfAnimationNodeType Type
    {
        get
        {
            string name = Node.LocalName;
            return name switch
            {
                "seq" => OdfAnimationNodeType.Sequence,
                "par" => OdfAnimationNodeType.Parallel,
                _ => OdfAnimationNodeType.Effect
            };
        }
    }

    /// <summary>
    /// Gets or sets the animation start time or trigger condition.
    /// 取得或設定動畫的開始時間或觸發條件。
    /// </summary>
    public string? Begin
    {
        get => Node.GetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            }
            else
            {
                Node.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }
    }

    /// <summary>
    /// Gets or sets the animation duration.
    /// 取得或設定動畫的持續時間。
    /// </summary>
    public string? Dur
    {
        get => Node.GetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            }
            else
            {
                Node.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }
    }

    /// <summary>
    /// Gets or sets the animation target element identifier.
    /// 取得或設定動畫的目標元素識別碼。
    /// </summary>
    public string? TargetElement
    {
        get => Node.GetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            }
            else
            {
                Node.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }
    }

    /// <summary>
    /// Gets the read-only list of child animation nodes.
    /// 取得子動畫節點的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfAnimationNode> Children
    {
        get
        {
            List<OdfAnimationNode> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri is "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
                {
                    list.Add(new OdfAnimationNode(child));
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// Adds a sequential animation sequence.
    /// 新增一個順序動畫序列。
    /// </summary>
    /// <param name="begin">The start-time attribute value. / 開始時間的屬性值。</param>
    /// <returns>The added sequential animation sequence node. / 新增的順序動畫序列節點。</returns>
    public OdfAnimationNode AddSequence(string? begin = null)
    {
        OdfNode seq = new(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        if (begin is not null)
        {
            seq.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", begin, "smil");
        }
        Node.AppendChild(seq);
        return new OdfAnimationNode(seq);
    }

    /// <summary>
    /// Adds a parallel animation sequence.
    /// 新增一個並行動畫序列。
    /// </summary>
    /// <param name="begin">The start-time attribute value. / 開始時間的屬性值。</param>
    /// <returns>The added parallel animation sequence node. / 新增的並行動畫序列節點。</returns>
    public OdfAnimationNode AddParallel(string? begin = null)
    {
        OdfNode par = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        if (begin is not null)
        {
            par.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", begin, "smil");
        }
        Node.AppendChild(par);
        return new OdfAnimationNode(par);
    }

    /// <summary>
    /// Adds an animation effect.
    /// 新增一個動畫效果。
    /// </summary>
    /// <param name="effectType">The animation effect type. / 動畫效果類型。</param>
    /// <param name="targetElementId">The target element identifier. / 目標元素識別碼。</param>
    /// <param name="duration">The duration. / 持續時間。</param>
    /// <param name="delay">The delay. / 延遲時間。</param>
    /// <returns>The added animation effect node. / 新增的動畫效果節點。</returns>
    public OdfAnimationNode AddEffect(OdfAnimationType effectType, string targetElementId, OdfLength duration, OdfLength delay)
    {
        OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetElementId, "smil");

        string durStr = $"{duration.ToPoints() / 72.0:F2}s";
        string delayStr = $"{delay.ToPoints() / 72.0:F2}s";

        string typeStr = "fade";
        string? subtypeStr = null;
        string modeStr = "in";

        switch (effectType)
        {
            case OdfAnimationType.FadeIn:
                typeStr = "fade";
                modeStr = "in";
                break;
            case OdfAnimationType.FadeOut:
                typeStr = "fade";
                modeStr = "out";
                break;
            case OdfAnimationType.ZoomIn:
                typeStr = "zoom";
                modeStr = "in";
                break;
            case OdfAnimationType.WipeRight:
                typeStr = "wipe";
                subtypeStr = "leftToRight";
                modeStr = "in";
                break;
        }

        filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", typeStr, "smil");
        if (subtypeStr is not null)
        {
            filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", subtypeStr, "smil");
        }
        filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
        filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", modeStr, "smil");

        Node.AppendChild(filter);
        return new OdfAnimationNode(filter);
    }
}


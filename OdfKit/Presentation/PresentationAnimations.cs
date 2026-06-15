using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示 ODF 動畫節點型態的列舉。
/// </summary>
public enum OdfAnimationNodeType
{
    /// <summary>
    /// 順序起動的動畫序列。
    /// </summary>
    Sequence,

    /// <summary>
    /// 同時起動的並行動畫序列。
    /// </summary>
    Parallel,

    /// <summary>
    /// 單一動畫效果。
    /// </summary>
    Effect
}

/// <summary>
/// 表示 ODF 動畫節點的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
public class OdfAnimationNode(OdfNode node)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
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
    /// 新增一個順序動畫序列。
    /// </summary>
    /// <param name="begin">開始時間的屬性值</param>
    /// <returns>新增的順序動畫序列節點</returns>
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
    /// 新增一個並行動畫序列。
    /// </summary>
    /// <param name="begin">開始時間的屬性值</param>
    /// <returns>新增的並行動畫序列節點</returns>
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
    /// 新增一個動畫效果。
    /// </summary>
    /// <param name="effectType">動畫效果類型</param>
    /// <param name="targetElementId">目標元素識別碼</param>
    /// <param name="duration">持續時間</param>
    /// <param name="delay">延遲時間</param>
    /// <returns>新增的動畫效果節點</returns>
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

/// <summary>
/// 表示高階動畫效果類型的列舉。
/// </summary>
public enum OdfAnimationEffect
{
    /// <summary>
    /// 出現。
    /// </summary>
    Appear,

    /// <summary>
    /// 淡入或淡出。
    /// </summary>
    Fade,

    /// <summary>
    /// 放大或縮小。
    /// </summary>
    Zoom,

    /// <summary>
    /// 飛入或飛出。
    /// </summary>
    FlyIn
}

/// <summary>
/// 表示高階動畫觸發方式的列舉。
/// </summary>
public enum OdfAnimationTrigger
{
    /// <summary>
    /// 滑鼠點擊時觸發。
    /// </summary>
    OnClick,

    /// <summary>
    /// 與前一個動畫同時執行。
    /// </summary>
    WithPrevious,

    /// <summary>
    /// 在前一個動畫結束後執行。
    /// </summary>
    AfterPrevious
}

/// <summary>
/// 表示投影片切換效果類型的列舉。
/// </summary>
public enum OdfSlideTransition
{
    /// <summary>
    /// 無切換效果。
    /// </summary>
    None,

    /// <summary>
    /// 淡出。
    /// </summary>
    Fade,

    /// <summary>
    /// 推入。
    /// </summary>
    Push,

    /// <summary>
    /// 擦去。
    /// </summary>
    Wipe,

    /// <summary>
    /// 縮放。
    /// </summary>
    Zoom
}

/// <summary>
/// 表示高階簡報動畫效果的類別。
/// </summary>
public sealed class OdfAnimation
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

public partial class OdfSlide
{
    /// <summary>
    /// 為指定的圖形新增物件進場動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼。</param>
    /// <param name="effect">動畫效果類型。</param>
    /// <param name="trigger">動畫觸發方式。</param>
    /// <param name="delay">動畫延遲啟動時間。</param>
    /// <returns>新增的動畫物件執行個體。</returns>
    public OdfAnimation AddEntranceEffect(string shapeId, OdfAnimationEffect effect, OdfAnimationTrigger trigger, TimeSpan delay = default)
    {
        if (string.IsNullOrEmpty(shapeId)) throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        var stepPar = FindOrCreateStepParNode(mainSeq, trigger);

        string delayStr = $"{delay.TotalSeconds:F2}s";
        string durStr = "0.5s";

        // 建立動畫效果包裝節點
        OdfNode effectPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        
        string beginVal = trigger switch
        {
            OdfAnimationTrigger.AfterPrevious => "prev.end" + (delay > TimeSpan.Zero ? $"+{delayStr}" : ""),
            _ => delayStr
        };
        effectPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", beginVal, "smil");
        effectPar.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        effectPar.SetAttribute("preset-class", OdfKit.Core.OdfNamespaces.Presentation, "entrance", "presentation");

        string presetId = effect switch
        {
            OdfAnimationEffect.Fade => "ooo-entrance-fade-in",
            OdfAnimationEffect.Zoom => "ooo-entrance-zoom-in",
            OdfAnimationEffect.FlyIn => "ooo-entrance-fly-in",
            _ => "ooo-entrance-appear"
        };
        effectPar.SetAttribute("preset-id", OdfKit.Core.OdfNamespaces.Presentation, presetId, "presentation");
        stepPar.AppendChild(effectPar);

        // 1. 建立 visibility 設為 visible 的動作，以保證簡報開始時隱藏，進場時才顯示
        OdfNode setVisible = new(OdfNodeType.Element, "set", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        setVisible.SetAttribute("attributeName", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "visibility", "smil");
        setVisible.SetAttribute("to", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "visible", "smil");
        setVisible.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
        setVisible.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0.001s", "smil");
        setVisible.SetAttribute("fill", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "hold", "smil");
        setVisible.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", shapeId, "smil");
        effectPar.AppendChild(setVisible);

        // 2. 建立具體效果 transitionFilter 節點
        OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", shapeId, "smil");
        filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
        filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");

        string typeVal = effect switch
        {
            OdfAnimationEffect.Fade => "fade",
            OdfAnimationEffect.Zoom => "zoom",
            OdfAnimationEffect.FlyIn => "fly",
            _ => "appear"
        };
        filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", typeVal, "smil");
        if (effect == OdfAnimationEffect.FlyIn)
        {
            filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fromLeft", "smil");
        }
        effectPar.AppendChild(filter);

        return new OdfAnimation(effectPar, shapeId, effect, trigger);
    }

    /// <summary>
    /// 為指定的圖形新增物件退場動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼。</param>
    /// <param name="effect">動畫效果類型。</param>
    /// <param name="trigger">動畫觸發方式。</param>
    /// <param name="delay">動畫延遲啟動時間。</param>
    /// <returns>新增的動畫物件執行個體。</returns>
    public OdfAnimation AddExitEffect(string shapeId, OdfAnimationEffect effect, OdfAnimationTrigger trigger, TimeSpan delay = default)
    {
        if (string.IsNullOrEmpty(shapeId)) throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        var stepPar = FindOrCreateStepParNode(mainSeq, trigger);

        string delayStr = $"{delay.TotalSeconds:F2}s";
        string durStr = "0.5s";

        // 建立動畫效果包裝節點
        OdfNode effectPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        
        string beginVal = trigger switch
        {
            OdfAnimationTrigger.AfterPrevious => "prev.end" + (delay > TimeSpan.Zero ? $"+{delayStr}" : ""),
            _ => delayStr
        };
        effectPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", beginVal, "smil");
        effectPar.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        effectPar.SetAttribute("preset-class", OdfKit.Core.OdfNamespaces.Presentation, "exit", "presentation");

        string presetId = effect switch
        {
            OdfAnimationEffect.Fade => "ooo-exit-fade-out",
            OdfAnimationEffect.Zoom => "ooo-exit-zoom-out",
            OdfAnimationEffect.FlyIn => "ooo-exit-fly-out",
            _ => "ooo-exit-disappear"
        };
        effectPar.SetAttribute("preset-id", OdfKit.Core.OdfNamespaces.Presentation, presetId, "presentation");
        stepPar.AppendChild(effectPar);

        // 1. 建立具體效果 transitionFilter 節點
        OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", shapeId, "smil");
        filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
        filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "out", "smil");

        string typeVal = effect switch
        {
            OdfAnimationEffect.Fade => "fade",
            OdfAnimationEffect.Zoom => "zoom",
            OdfAnimationEffect.FlyIn => "fly",
            _ => "appear"
        };
        filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", typeVal, "smil");
        if (effect == OdfAnimationEffect.FlyIn)
        {
            filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "toLeft", "smil");
        }
        effectPar.AppendChild(filter);

        // 2. 建立 visibility 設為 hidden 的動作，以保證退場完成後隱藏
        OdfNode setHidden = new(OdfNodeType.Element, "set", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        setHidden.SetAttribute("attributeName", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "visibility", "smil");
        setHidden.SetAttribute("to", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "hidden", "smil");
        setHidden.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        setHidden.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0.001s", "smil");
        setHidden.SetAttribute("fill", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "hold", "smil");
        setHidden.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", shapeId, "smil");
        effectPar.AppendChild(setHidden);

        return new OdfAnimation(effectPar, shapeId, effect, trigger);
    }

    /// <summary>
    /// 為指定的圖形新增物件強調動畫。
    /// </summary>
    /// <param name="shapeId">目標圖形識別碼。</param>
    /// <param name="effect">動畫效果類型。</param>
    /// <returns>新增的動畫物件執行個體。</returns>
    public OdfAnimation AddEmphasisEffect(string shapeId, OdfAnimationEffect effect)
    {
        if (string.IsNullOrEmpty(shapeId)) throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        // 強調動畫一般為點擊觸發
        var stepPar = FindOrCreateStepParNode(mainSeq, OdfAnimationTrigger.OnClick);

        string durStr = "0.5s";

        // 建立動畫效果包裝節點
        OdfNode effectPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        effectPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
        effectPar.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        effectPar.SetAttribute("preset-class", OdfKit.Core.OdfNamespaces.Presentation, "emphasis", "presentation");
        effectPar.SetAttribute("preset-id", OdfKit.Core.OdfNamespaces.Presentation, "ooo-emphasis-fade", "presentation");
        stepPar.AppendChild(effectPar);

        // 建立具體效果 transitionFilter 節點
        OdfNode filter = new(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", shapeId, "smil");
        filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
        filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
        filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "fade", "smil");
        filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "in", "smil");
        effectPar.AppendChild(filter);

        return new OdfAnimation(effectPar, shapeId, effect, OdfAnimationTrigger.OnClick);
    }

    private static OdfNode FindOrCreateStepParNode(OdfNode mainSeq, OdfAnimationTrigger trigger)
    {
        if (trigger == OdfAnimationTrigger.OnClick)
        {
            OdfNode clickPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            clickPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "on-click", "smil");
            clickPar.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "indefinite", "smil");
            
            // 需要用一個 anim:par smil:begin="0s" 來包裹這個點擊步驟
            OdfNode outerPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            outerPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");
            
            outerPar.AppendChild(clickPar);
            mainSeq.AppendChild(outerPar);
            return clickPar;
        }
        else
        {
            // 尋找最後一個 on-click par
            OdfNode? lastClickPar = FindLastClickPar(mainSeq);
            if (lastClickPar is not null)
            {
                return lastClickPar;
            }

            // 若找不到，則建立一個預設的 on-click 步驟
            OdfNode clickPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            clickPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "on-click", "smil");
            clickPar.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "indefinite", "smil");

            OdfNode outerPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            outerPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", "0s", "smil");

            outerPar.AppendChild(clickPar);
            mainSeq.AppendChild(outerPar);
            return clickPar;
        }
    }

    private static OdfNode? FindLastClickPar(OdfNode mainSeq)
    {
        for (int i = mainSeq.Children.Count - 1; i >= 0; i--)
        {
            var child = mainSeq.Children[i];
            if (child.LocalName == "par" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
            {
                foreach (var innerChild in child.Children)
                {
                    if (innerChild.LocalName == "par" &&
                        innerChild.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0" &&
                        innerChild.GetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0") == "on-click")
                    {
                        return innerChild;
                    }
                }
            }
        }
        return null;
    }
}

public partial class PresentationDocument
{
    /// <summary>
    /// 設定指定索引投影片的切換效果。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <param name="transition">投影片切換效果類型。</param>
    public void SetSlideTransition(int slideIndex, OdfSlideTransition transition)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
        }

        var slide = Slides[slideIndex];
        var slideNode = slide.Node;

        if (transition == OdfSlideTransition.None)
        {
            slideNode.RemoveAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("transition-type", OdfKit.Core.OdfNamespaces.Presentation);
        }
        else
        {
            OdfTransitionType type = transition switch
            {
                OdfSlideTransition.Push => OdfTransitionType.Push,
                OdfSlideTransition.Wipe => OdfTransitionType.Wipe,
                OdfSlideTransition.Zoom => OdfTransitionType.Zoom,
                _ => OdfTransitionType.Fade
            };
            slide.SetTransition(type, OdfLength.Parse("72pt"));
        }
    }
}

using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

public partial class OdfSlide
{
    #region Slide Animations

    /// <summary>
    /// Adds an entrance animation to the specified shape.
    /// 為指定的圖形新增物件進場動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect type. / 動畫效果類型。</param>
    /// <param name="trigger">The animation trigger mode. / 動畫觸發方式。</param>
    /// <param name="delay">The animation startup delay. / 動畫延遲啟動時間。</param>
    /// <param name="duration">The animation duration; defaults to 0.5 seconds. / 動畫持續時間；預設為 0.5 秒。</param>
    /// <returns>The added animation object instance. / 新增的動畫物件執行個體。</returns>
    public OdfAnimation AddEntranceEffect(
        string shapeId,
        OdfAnimationEffect effect,
        OdfAnimationTrigger trigger,
        TimeSpan delay = default,
        TimeSpan duration = default)
    {
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSlide_TargetCannotBeEmpty_3"), nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        var stepPar = FindOrCreateStepParNode(mainSeq, trigger);

        TimeSpan effectiveDuration = duration == default ? TimeSpan.FromSeconds(0.5) : duration;
        string delayStr = OdfSmilTime.FormatDelay(delay);
        string durStr = OdfSmilTime.FormatDuration(effectiveDuration);

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
    /// Adds an exit animation to the specified shape.
    /// 為指定的圖形新增物件退場動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect type. / 動畫效果類型。</param>
    /// <param name="trigger">The animation trigger mode. / 動畫觸發方式。</param>
    /// <param name="delay">The animation startup delay. / 動畫延遲啟動時間。</param>
    /// <param name="duration">The animation duration; defaults to 0.5 seconds. / 動畫持續時間；預設為 0.5 秒。</param>
    /// <returns>The added animation object instance. / 新增的動畫物件執行個體。</returns>
    public OdfAnimation AddExitEffect(
        string shapeId,
        OdfAnimationEffect effect,
        OdfAnimationTrigger trigger,
        TimeSpan delay = default,
        TimeSpan duration = default)
    {
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSlide_TargetCannotBeEmpty_3"), nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        var stepPar = FindOrCreateStepParNode(mainSeq, trigger);

        TimeSpan effectiveDuration = duration == default ? TimeSpan.FromSeconds(0.5) : duration;
        string delayStr = OdfSmilTime.FormatDelay(delay);
        string durStr = OdfSmilTime.FormatDuration(effectiveDuration);

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
    /// Adds an emphasis animation to the specified shape.
    /// 為指定的圖形新增物件強調動畫。
    /// </summary>
    /// <param name="shapeId">The target shape identifier. / 目標圖形識別碼。</param>
    /// <param name="effect">The animation effect type. / 動畫效果類型。</param>
    /// <param name="duration">The animation duration; defaults to 0.5 seconds. / 動畫持續時間；預設為 0.5 秒。</param>
    /// <param name="trigger">The animation trigger mode. / 動畫觸發方式。</param>
    /// <param name="delay">The animation startup delay. / 動畫延遲啟動時間。</param>
    /// <returns>The added animation object instance. / 新增的動畫物件執行個體。</returns>
    public OdfAnimation AddEmphasisEffect(
        string shapeId,
        OdfAnimationEffect effect,
        TimeSpan duration = default,
        OdfAnimationTrigger trigger = OdfAnimationTrigger.OnClick,
        TimeSpan delay = default)
    {
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSlide_TargetCannotBeEmpty_3"), nameof(shapeId));

        var mainSeq = AnimationRoot.Node;
        // 強調動畫預設為點擊觸發，但匯入 PPTX 時需保留接續上一個效果的時序。
        var stepPar = FindOrCreateStepParNode(mainSeq, trigger);

        TimeSpan effectiveDuration = duration == default ? TimeSpan.FromSeconds(0.5) : duration;
        string delayStr = OdfSmilTime.FormatDelay(delay);
        string durStr = OdfSmilTime.FormatDuration(effectiveDuration);

        // 建立動畫效果包裝節點
        OdfNode effectPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
        string beginVal = trigger switch
        {
            OdfAnimationTrigger.AfterPrevious => "prev.end" + (delay > TimeSpan.Zero ? $"+{delayStr}" : ""),
            _ => delay > TimeSpan.Zero ? delayStr : "0s",
        };
        effectPar.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", beginVal, "smil");
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

        return new OdfAnimation(effectPar, shapeId, effect, trigger);
    }

    private static OdfNode FindOrCreateStepParNode(OdfNode mainSeq, OdfAnimationTrigger trigger)
    {
        if (trigger == OdfAnimationTrigger.OnClick)
        {
            OdfNode clickPar = new(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            clickPar.SetAttribute("node-type", OdfKit.Core.OdfNamespaces.Presentation, "on-click", "presentation");

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
            clickPar.SetAttribute("node-type", OdfKit.Core.OdfNamespaces.Presentation, "on-click", "presentation");

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
                        innerChild.GetAttribute("node-type", OdfKit.Core.OdfNamespaces.Presentation) == "on-click")
                    {
                        return innerChild;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the summary list of all animation effects on this slide.
    /// 取得此投影片上所有動畫效果的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfAnimationInfo> GetAnimations() =>
        OdfSlideAnimationReadEngine.GetAnimations(this);

    #endregion
}

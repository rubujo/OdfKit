using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

public partial class OdfSlide
{
    #region Slide Animations

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
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

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
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

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
        if (string.IsNullOrEmpty(shapeId))
            throw new ArgumentException("目標圖形識別碼不可為空。", nameof(shapeId));

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

    #endregion
}

using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents high-level animation effect types.
/// 表示高階動畫效果類型的列舉。
/// </summary>
public enum OdfAnimationEffect
{
    /// <summary>
    /// Appear.
    /// 出現。
    /// </summary>
    Appear,

    /// <summary>
    /// Fade in or fade out.
    /// 淡入或淡出。
    /// </summary>
    Fade,

    /// <summary>
    /// Zoom in or zoom out.
    /// 放大或縮小。
    /// </summary>
    Zoom,

    /// <summary>
    /// Fly in or fly out.
    /// 飛入或飛出。
    /// </summary>
    FlyIn
}

/// <summary>
/// Represents high-level animation trigger modes.
/// 表示高階動畫觸發方式的列舉。
/// </summary>
public enum OdfAnimationTrigger
{
    /// <summary>
    /// Trigger on mouse click.
    /// 滑鼠點擊時觸發。
    /// </summary>
    OnClick,

    /// <summary>
    /// Run with the previous animation.
    /// 與前一個動畫同時執行。
    /// </summary>
    WithPrevious,

    /// <summary>
    /// Run after the previous animation ends.
    /// 在前一個動畫結束後執行。
    /// </summary>
    AfterPrevious
}

/// <summary>
/// Represents slide transition effect types.
/// 表示投影片切換效果類型的列舉。
/// </summary>
public enum OdfSlideTransition
{
    /// <summary>
    /// No transition effect.
    /// 無切換效果。
    /// </summary>
    None,

    /// <summary>
    /// Fade transition.
    /// 淡出。
    /// </summary>
    Fade,

    /// <summary>
    /// Push transition.
    /// 推入。
    /// </summary>
    Push,

    /// <summary>
    /// Wipe transition.
    /// 擦去。
    /// </summary>
    Wipe,

    /// <summary>
    /// Zoom transition.
    /// 縮放。
    /// </summary>
    Zoom
}


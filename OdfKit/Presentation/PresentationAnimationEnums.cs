using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

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


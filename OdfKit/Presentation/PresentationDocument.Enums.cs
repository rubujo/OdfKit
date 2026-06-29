using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents slide page orientations.
/// 表示投影片頁面方向的列舉。
/// </summary>
public enum OdfPageOrientation
{
    /// <summary>
    /// Landscape orientation.
    /// 橫向。
    /// </summary>
    Landscape,

    /// <summary>
    /// Portrait orientation.
    /// 直向。
    /// </summary>
    Portrait
}

/// <summary>
/// Represents shape types.
/// 表示圖形種類的列舉。
/// </summary>
public enum OdfShapeType
{
    /// <summary>
    /// Rectangle.
    /// 矩形。
    /// </summary>
    Rectangle,

    /// <summary>
    /// Ellipse.
    /// 橢圓形。
    /// </summary>
    Ellipse,

    /// <summary>
    /// Custom shape.
    /// 自訂圖形。
    /// </summary>
    Custom
}

/// <summary>
/// Represents slide transition effect types.
/// 表示投影片切換效果類型的列舉。
/// </summary>
public enum OdfTransitionType
{
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
    Zoom,

    /// <summary>
    /// Split transition.
    /// 分割。
    /// </summary>
    Split
}

/// <summary>
/// Represents slide transition speeds.
/// 表示投影片切換速度。
/// </summary>
public enum OdfTransitionSpeed
{
    /// <summary>
    /// Slow speed.
    /// 慢速。
    /// </summary>
    Slow,

    /// <summary>
    /// Medium speed.
    /// 中速。
    /// </summary>
    Medium,

    /// <summary>
    /// Fast speed.
    /// 快速。
    /// </summary>
    Fast
}

/// <summary>
/// Represents animation effect types.
/// 表示動畫效果類型的列舉。
/// </summary>
public enum OdfAnimationType
{
    /// <summary>
    /// Fade-in animation.
    /// 淡入。
    /// </summary>
    FadeIn,

    /// <summary>
    /// Fade-out animation.
    /// 淡出。
    /// </summary>
    FadeOut,

    /// <summary>
    /// Zoom-in animation.
    /// 放大。
    /// </summary>
    ZoomIn,

    /// <summary>
    /// Wipe-right animation.
    /// 向右擦去。
    /// </summary>
    WipeRight
}

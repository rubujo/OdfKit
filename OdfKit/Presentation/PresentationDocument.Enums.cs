using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片頁面方向的列舉。
/// </summary>
public enum OdfPageOrientation
{
    /// <summary>
    /// 橫向。
    /// </summary>
    Landscape,

    /// <summary>
    /// 直向。
    /// </summary>
    Portrait
}

/// <summary>
/// 表示圖形種類的列舉。
/// </summary>
public enum OdfShapeType
{
    /// <summary>
    /// 矩形。
    /// </summary>
    Rectangle,

    /// <summary>
    /// 橢圓形。
    /// </summary>
    Ellipse,

    /// <summary>
    /// 自訂圖形。
    /// </summary>
    Custom
}

/// <summary>
/// 表示投影片切換效果類型的列舉。
/// </summary>
public enum OdfTransitionType
{
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
    Zoom,

    /// <summary>
    /// 分割。
    /// </summary>
    Split
}

/// <summary>
/// 表示動畫效果類型的列舉。
/// </summary>
public enum OdfAnimationType
{
    /// <summary>
    /// 淡入。
    /// </summary>
    FadeIn,

    /// <summary>
    /// 淡出。
    /// </summary>
    FadeOut,

    /// <summary>
    /// 放大。
    /// </summary>
    ZoomIn,

    /// <summary>
    /// 向右擦去。
    /// </summary>
    WipeRight
}

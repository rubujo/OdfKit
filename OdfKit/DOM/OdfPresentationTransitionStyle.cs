namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>presentation:transition-style</c> 的轉場樣式 token。
/// </summary>
public enum OdfPresentationTransitionStyle
{
    /// <summary>
    /// 無轉場樣式。
    /// </summary>
    None,

    /// <summary>
    /// 從左側淡入。
    /// </summary>
    FadeFromLeft,

    /// <summary>
    /// 從上方淡入。
    /// </summary>
    FadeFromTop,

    /// <summary>
    /// 從右側淡入。
    /// </summary>
    FadeFromRight,

    /// <summary>
    /// 從下方淡入。
    /// </summary>
    FadeFromBottom,

    /// <summary>
    /// 從左上方淡入。
    /// </summary>
    FadeFromUpperLeft,

    /// <summary>
    /// 從右上方淡入。
    /// </summary>
    FadeFromUpperRight,

    /// <summary>
    /// 從左下方淡入。
    /// </summary>
    FadeFromLowerLeft,

    /// <summary>
    /// 從右下方淡入。
    /// </summary>
    FadeFromLowerRight,

    /// <summary>
    /// 從左側移入。
    /// </summary>
    MoveFromLeft,

    /// <summary>
    /// 從上方移入。
    /// </summary>
    MoveFromTop,

    /// <summary>
    /// 從右側移入。
    /// </summary>
    MoveFromRight,

    /// <summary>
    /// 從下方移入。
    /// </summary>
    MoveFromBottom,

    /// <summary>
    /// 從左上方移入。
    /// </summary>
    MoveFromUpperLeft,

    /// <summary>
    /// 從右上方移入。
    /// </summary>
    MoveFromUpperRight,

    /// <summary>
    /// 從左下方移入。
    /// </summary>
    MoveFromLowerLeft,

    /// <summary>
    /// 從右下方移入。
    /// </summary>
    MoveFromLowerRight,

    /// <summary>
    /// 向左揭露。
    /// </summary>
    UncoverToLeft,

    /// <summary>
    /// 向上揭露。
    /// </summary>
    UncoverToTop,

    /// <summary>
    /// 向右揭露。
    /// </summary>
    UncoverToRight,

    /// <summary>
    /// 向下揭露。
    /// </summary>
    UncoverToBottom,

    /// <summary>
    /// 向左上方揭露。
    /// </summary>
    UncoverToUpperLeft,

    /// <summary>
    /// 向右上方揭露。
    /// </summary>
    UncoverToUpperRight,

    /// <summary>
    /// 向左下方揭露。
    /// </summary>
    UncoverToLowerLeft,

    /// <summary>
    /// 向右下方揭露。
    /// </summary>
    UncoverToLowerRight,

    /// <summary>
    /// 淡入至中心。
    /// </summary>
    FadeToCenter,

    /// <summary>
    /// 從中心淡入。
    /// </summary>
    FadeFromCenter,

    /// <summary>
    /// 垂直條紋。
    /// </summary>
    VerticalStripes,

    /// <summary>
    /// 水平條紋。
    /// </summary>
    HorizontalStripes,

    /// <summary>
    /// 順時針。
    /// </summary>
    Clockwise,

    /// <summary>
    /// 逆時針。
    /// </summary>
    Counterclockwise,

    /// <summary>
    /// 垂直開啟。
    /// </summary>
    OpenVertical,

    /// <summary>
    /// 水平開啟。
    /// </summary>
    OpenHorizontal,

    /// <summary>
    /// 垂直關閉。
    /// </summary>
    CloseVertical,

    /// <summary>
    /// 水平關閉。
    /// </summary>
    CloseHorizontal,

    /// <summary>
    /// 從左側產生波浪線。
    /// </summary>
    WavylineFromLeft,

    /// <summary>
    /// 從上方產生波浪線。
    /// </summary>
    WavylineFromTop,

    /// <summary>
    /// 從右側產生波浪線。
    /// </summary>
    WavylineFromRight,

    /// <summary>
    /// 從下方產生波浪線。
    /// </summary>
    WavylineFromBottom,

    /// <summary>
    /// 向內左旋。
    /// </summary>
    SpiralinLeft,

    /// <summary>
    /// 向內右旋。
    /// </summary>
    SpiralinRight,

    /// <summary>
    /// 向外左旋。
    /// </summary>
    SpiraloutLeft,

    /// <summary>
    /// 向外右旋。
    /// </summary>
    SpiraloutRight,

    /// <summary>
    /// 從上方捲入。
    /// </summary>
    RollFromTop,

    /// <summary>
    /// 從左側捲入。
    /// </summary>
    RollFromLeft,

    /// <summary>
    /// 從右側捲入。
    /// </summary>
    RollFromRight,

    /// <summary>
    /// 從下方捲入。
    /// </summary>
    RollFromBottom,

    /// <summary>
    /// 從左側伸展。
    /// </summary>
    StretchFromLeft,

    /// <summary>
    /// 從上方伸展。
    /// </summary>
    StretchFromTop,

    /// <summary>
    /// 從右側伸展。
    /// </summary>
    StretchFromRight,

    /// <summary>
    /// 從下方伸展。
    /// </summary>
    StretchFromBottom,

    /// <summary>
    /// 垂直線條。
    /// </summary>
    VerticalLines,

    /// <summary>
    /// 水平線條。
    /// </summary>
    HorizontalLines,

    /// <summary>
    /// 溶解。
    /// </summary>
    Dissolve,

    /// <summary>
    /// 隨機。
    /// </summary>
    Random,

    /// <summary>
    /// 垂直棋盤。
    /// </summary>
    VerticalCheckerboard,

    /// <summary>
    /// 水平棋盤。
    /// </summary>
    HorizontalCheckerboard,

    /// <summary>
    /// 水平向左交錯。
    /// </summary>
    InterlockingHorizontalLeft,

    /// <summary>
    /// 水平向右交錯。
    /// </summary>
    InterlockingHorizontalRight,

    /// <summary>
    /// 垂直向上交錯。
    /// </summary>
    InterlockingVerticalTop,

    /// <summary>
    /// 垂直向下交錯。
    /// </summary>
    InterlockingVerticalBottom,

    /// <summary>
    /// 飛離。
    /// </summary>
    FlyAway,

    /// <summary>
    /// 開啟。
    /// </summary>
    Open,

    /// <summary>
    /// 關閉。
    /// </summary>
    Close,

    /// <summary>
    /// 融化。
    /// </summary>
    Melt
}

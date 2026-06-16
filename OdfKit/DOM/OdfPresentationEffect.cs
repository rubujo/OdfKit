namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>presentation:effect</c> 的簡報效果 token。
/// </summary>
public enum OdfPresentationEffect
{
    /// <summary>
    /// 無效果。
    /// </summary>
    None,

    /// <summary>
    /// 淡化效果。
    /// </summary>
    Fade,

    /// <summary>
    /// 移動效果。
    /// </summary>
    Move,

    /// <summary>
    /// 條紋效果。
    /// </summary>
    Stripes,

    /// <summary>
    /// 開啟效果。
    /// </summary>
    Open,

    /// <summary>
    /// 關閉效果。
    /// </summary>
    Close,

    /// <summary>
    /// 溶解效果。
    /// </summary>
    Dissolve,

    /// <summary>
    /// 波浪線效果。
    /// </summary>
    Wavyline,

    /// <summary>
    /// 隨機效果。
    /// </summary>
    Random,

    /// <summary>
    /// 線條效果。
    /// </summary>
    Lines,

    /// <summary>
    /// 雷射效果。
    /// </summary>
    Laser,

    /// <summary>
    /// 出現效果。
    /// </summary>
    Appear,

    /// <summary>
    /// 隱藏效果。
    /// </summary>
    Hide,

    /// <summary>
    /// 短距離移動效果。
    /// </summary>
    MoveShort,

    /// <summary>
    /// 棋盤效果。
    /// </summary>
    Checkerboard,

    /// <summary>
    /// 旋轉效果。
    /// </summary>
    Rotate,

    /// <summary>
    /// 伸展效果。
    /// </summary>
    Stretch
}

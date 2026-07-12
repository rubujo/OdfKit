namespace OdfKit.Drawing;

/// <summary>
/// Defines alignment modes for drawing shapes.
/// 定義繪圖圖形的對齊模式。
/// </summary>
public enum OdfShapeAlignment
{
    /// <summary>
    /// Aligns the left edges.
    /// 對齊左側邊緣。
    /// </summary>
    Left,

    /// <summary>
    /// Aligns the horizontal centers.
    /// 對齊水平中心。
    /// </summary>
    Center,

    /// <summary>
    /// Aligns the right edges.
    /// 對齊右側邊緣。
    /// </summary>
    Right,

    /// <summary>
    /// Aligns the top edges.
    /// 對齊頂端邊緣。
    /// </summary>
    Top,

    /// <summary>
    /// Aligns the vertical centers.
    /// 對齊垂直中心。
    /// </summary>
    Middle,

    /// <summary>
    /// Aligns the bottom edges.
    /// 對齊底端邊緣。
    /// </summary>
    Bottom
}

/// <summary>
/// Defines distribution directions for drawing shapes.
/// 定義繪圖圖形的等距分布方向。
/// </summary>
public enum OdfShapeDistribution
{
    /// <summary>
    /// Distributes shapes horizontally with equal gaps.
    /// 以相等間距水平分布圖形。
    /// </summary>
    Horizontal,

    /// <summary>
    /// Distributes shapes vertically with equal gaps.
    /// 以相等間距垂直分布圖形。
    /// </summary>
    Vertical
}

namespace OdfKit.Styles;

/// <summary>
/// 表示高階 builder 版面區塊的公分座標。
/// </summary>
public readonly struct OdfLayoutBounds
{
    /// <summary>
    /// 初始化 <see cref="OdfLayoutBounds"/> 結構的新執行個體。
    /// </summary>
    /// <param name="xCm">左側位置（公分）</param>
    /// <param name="yCm">上方位置（公分）</param>
    /// <param name="widthCm">寬度（公分）</param>
    /// <param name="heightCm">高度（公分）</param>
    public OdfLayoutBounds(double xCm, double yCm, double widthCm, double heightCm)
    {
        XCm = xCm;
        YCm = yCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
    }

    /// <summary>
    /// 取得左側位置（公分）。
    /// </summary>
    public double XCm { get; }

    /// <summary>
    /// 取得上方位置（公分）。
    /// </summary>
    public double YCm { get; }

    /// <summary>
    /// 取得寬度（公分）。
    /// </summary>
    public double WidthCm { get; }

    /// <summary>
    /// 取得高度（公分）。
    /// </summary>
    public double HeightCm { get; }
}

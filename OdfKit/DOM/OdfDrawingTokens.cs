namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>dr3d:projection</c> 的 3D 投影 token。
/// </summary>
public enum OdfDr3dProjection
{
    /// <summary>
    /// 平行投影。
    /// </summary>
    Parallel,

    /// <summary>
    /// 透視投影。
    /// </summary>
    Perspective
}

/// <summary>
/// 表示 ODF schema 中 <c>dr3d:shade-mode</c> 的 3D 著色模式 token。
/// </summary>
public enum OdfDr3dShadeMode
{
    /// <summary>
    /// 草稿著色。
    /// </summary>
    Draft,

    /// <summary>
    /// 平面著色。
    /// </summary>
    Flat,

    /// <summary>
    /// Gouraud 著色。
    /// </summary>
    Gouraud,

    /// <summary>
    /// Phong 著色。
    /// </summary>
    Phong
}

/// <summary>
/// 表示 ODF schema 中 <c>svg:fill-rule</c> 的填滿規則 token。
/// </summary>
public enum OdfSvgFillRule
{
    /// <summary>
    /// Even-odd 填滿規則。
    /// </summary>
    EvenOdd,

    /// <summary>
    /// Nonzero 填滿規則。
    /// </summary>
    Nonzero
}

/// <summary>
/// 表示 ODF schema 中 <c>table:border-model</c> 的表格邊框模型 token。
/// </summary>
public enum OdfTableBorderModel
{
    /// <summary>
    /// 相鄰框線合併。
    /// </summary>
    Collapsing,

    /// <summary>
    /// 相鄰框線分離。
    /// </summary>
    Separating
}

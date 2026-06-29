using System;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Specifies the usage of a page.
/// 指定頁面的使用方式。
/// </summary>
public enum OdfPageUsage
{
    /// <summary>
    /// 套用至所有頁面（預設）
    /// </summary>
    All,
    /// <summary>
    /// 僅套用至左側頁面
    /// </summary>
    Left,
    /// <summary>
    /// 僅套用至右側頁面
    /// </summary>
    Right,
    /// <summary>
    /// 鏡像頁面，左右交替
    /// </summary>
    Mirrored,
}

/// <summary>
/// Specifies the mode of a layout grid.
/// 指定版面配置網格的模式。
/// </summary>
public enum OdfLayoutGridMode
{
    /// <summary>
    /// 無網格
    /// </summary>
    None,
    /// <summary>
    /// 僅顯示行網格
    /// </summary>
    Line,
    /// <summary>
    /// 顯示行列網格
    /// </summary>
    Both,
}

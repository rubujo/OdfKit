using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies icon set types for icon set conditional formatting.
/// 圖示集條件格式的圖示集類型。
/// </summary>
public enum OdfIconSetType
{
    /// <summary>
    /// Three arrows.
    /// 三箭頭（↑ →↓）。
    /// </summary>
    ThreeArrows,

    /// <summary>
    /// Three traffic lights.
    /// 三交通燈（●●●）。
    /// </summary>
    ThreeTrafficLights,

    /// <summary>
    /// Four rating icons.
    /// 四評分（★★★★）。
    /// </summary>
    FourRating,

    /// <summary>
    /// Five rating icons.
    /// 五評分（★★★★★）。
    /// </summary>
    FiveRating,
}

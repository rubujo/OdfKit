using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 圖示集條件格式的圖示集類型。
/// </summary>
public enum OdfIconSetType
{
    /// <summary>
    /// 三箭頭（↑ →↓）
    /// </summary>
    ThreeArrows,

    /// <summary>
    /// 三交通燈（●●●）
    /// </summary>
    ThreeTrafficLights,

    /// <summary>
    /// 四評分（★★★★）
    /// </summary>
    FourRating,

    /// <summary>
    /// 五評分（★★★★★）
    /// </summary>
    FiveRating,
}

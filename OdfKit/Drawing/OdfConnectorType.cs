using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// 表示連接線幾何類型的列舉。
/// </summary>
public enum OdfConnectorType
{
    /// <summary>
    /// 標準折線連接線。
    /// </summary>
    Standard,

    /// <summary>
    /// 多段折線連接線。
    /// </summary>
    Lines,

    /// <summary>
    /// 直線連接線。
    /// </summary>
    Straight,

    /// <summary>
    /// 曲線連接線。
    /// </summary>
    Curve
}

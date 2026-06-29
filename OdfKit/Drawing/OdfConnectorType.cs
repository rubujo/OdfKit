using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// Enumerates connector geometry types.
/// 表示連接線幾何類型的列舉。
/// </summary>
public enum OdfConnectorType
{
    /// <summary>
    /// A standard bent-line connector.
    /// 標準折線連接線。
    /// </summary>
    Standard,

    /// <summary>
    /// A multi-segment polyline connector.
    /// 多段折線連接線。
    /// </summary>
    Lines,

    /// <summary>
    /// A straight-line connector.
    /// 直線連接線。
    /// </summary>
    Straight,

    /// <summary>
    /// A curved connector.
    /// 曲線連接線。
    /// </summary>
    Curve
}

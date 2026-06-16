using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示 ODF 動畫節點型態的列舉。
/// </summary>
public enum OdfAnimationNodeType
{
    /// <summary>
    /// 順序起動的動畫序列。
    /// </summary>
    Sequence,

    /// <summary>
    /// 同時起動的並行動畫序列。
    /// </summary>
    Parallel,

    /// <summary>
    /// 單一動畫效果。
    /// </summary>
    Effect
}


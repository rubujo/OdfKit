using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents ODF animation node types.
/// 表示 ODF 動畫節點型態的列舉。
/// </summary>
public enum OdfAnimationNodeType
{
    /// <summary>
    /// A sequential animation sequence.
    /// 順序起動的動畫序列。
    /// </summary>
    Sequence,

    /// <summary>
    /// A parallel animation sequence.
    /// 同時起動的並行動畫序列。
    /// </summary>
    Parallel,

    /// <summary>
    /// A single animation effect.
    /// 單一動畫效果。
    /// </summary>
    Effect
}


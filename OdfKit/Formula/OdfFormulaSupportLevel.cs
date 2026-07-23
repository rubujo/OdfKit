using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents the support level for a formula function.
/// 表示公式函式支援層級。
/// </summary>
public enum OdfFormulaSupportLevel
{
    /// <summary>
    /// The function can be evaluated by the default evaluator.
    /// 可由預設評估器計算。
    /// </summary>
    Evaluated = 0,

    /// <summary>
    /// The function can be preserved in documents but is not evaluated by the default evaluator.
    /// 可在文件中保真儲存，但預設評估器不計算。
    /// </summary>
    PreservedOnly = 1,

    /// <summary>
    /// The function has a bounded in-process implementation, but some specification semantics require additional context or corpus evidence.
    /// 函式具有受限的同處理程序實作，但部分規範語意仍需要額外內容模型或 corpus 證據。
    /// </summary>
    BestEffort = 2
}

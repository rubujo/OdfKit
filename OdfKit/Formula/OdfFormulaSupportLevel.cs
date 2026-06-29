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
    Evaluated,

    /// <summary>
    /// The function can be preserved in documents but is not evaluated by the default evaluator.
    /// 可在文件中保真保存，但預設評估器不計算。
    /// </summary>
    PreservedOnly
}

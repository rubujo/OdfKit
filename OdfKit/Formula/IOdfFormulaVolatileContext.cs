using System;

namespace OdfKit.Formula;

/// <summary>
/// Supplies calculation-session state for volatile OpenFormula functions.
/// 提供 OpenFormula volatile 函式所需的計算工作階段狀態。
/// </summary>
public interface IOdfFormulaVolatileContext
{
    /// <summary>
    /// Gets the local timestamp captured for the current calculation session.
    /// 取得目前計算工作階段所擷取的當地時間戳記。
    /// </summary>
    DateTime EvaluationTimestamp { get; }

    /// <summary>
    /// Returns the next uniformly distributed random value for the current calculation session.
    /// 傳回目前計算工作階段的下一個均勻分布隨機值。
    /// </summary>
    /// <remarks>
    /// Implementations used by parallel document recalculation must be thread-safe.
    /// 用於平行文件重算的實作必須確保執行緒安全。
    /// </remarks>
    /// <returns>A value greater than or equal to zero and less than one. / 大於或等於零且小於一的值。</returns>
    double NextRandomDouble();
}

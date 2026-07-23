using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// Reports mandatory-function coverage for an OpenFormula evaluator group.
/// 報告 OpenFormula 評估器群組的強制函式覆蓋情形。
/// </summary>
/// <remarks>
/// Complete function-name coverage is necessary but does not by itself prove conformance; syntax, limits, conversions, and semantics require separate evidence.
/// 完整函式名稱覆蓋是必要條件，但本身不足以證明一致性；語法、限制、轉換及語意仍須個別提出證據。
/// </remarks>
public sealed class OdfFormulaConformanceReport
{
    internal OdfFormulaConformanceReport(
        OdfFormulaConformanceGroup group,
        IReadOnlyList<string> requiredFunctions,
        IReadOnlyList<string> missingFunctions,
        IReadOnlyList<string> bestEffortFunctions)
    {
        Group = group;
        RequiredFunctions = requiredFunctions;
        MissingFunctions = missingFunctions;
        BestEffortFunctions = bestEffortFunctions;
    }

    /// <summary>
    /// Gets the evaluated conformance group.
    /// 取得接受評估的一致性群組。
    /// </summary>
    public OdfFormulaConformanceGroup Group { get; }

    /// <summary>
    /// Gets the cumulative mandatory function names.
    /// 取得累計強制函式名稱。
    /// </summary>
    public IReadOnlyList<string> RequiredFunctions { get; }

    /// <summary>
    /// Gets mandatory function names unavailable from the evaluated function set.
    /// 取得接受評估的函式集合尚未提供的強制函式名稱。
    /// </summary>
    public IReadOnlyList<string> MissingFunctions { get; }

    /// <summary>
    /// Gets mandatory functions whose built-in implementations are explicitly classified as best effort.
    /// 取得內建實作明確分類為 Best Effort 的強制函式。
    /// </summary>
    public IReadOnlyList<string> BestEffortFunctions { get; }

    /// <summary>
    /// Gets a value indicating whether every mandatory function name is available.
    /// 取得是否已提供全部強制函式名稱。
    /// </summary>
    public bool HasCompleteFunctionSet => MissingFunctions.Count == 0;

    /// <summary>
    /// Gets a value indicating whether every available mandatory function is classified as fully evaluated.
    /// 取得所有可用強制函式是否皆分類為完整求值。
    /// </summary>
    public bool HasOnlyFullyEvaluatedFunctions =>
        MissingFunctions.Count == 0 && BestEffortFunctions.Count == 0;
}

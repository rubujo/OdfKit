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
        IReadOnlyList<string> missingFunctions)
    {
        Group = group;
        RequiredFunctions = requiredFunctions;
        MissingFunctions = missingFunctions;
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
    /// Gets a value indicating whether every mandatory function name is available.
    /// 取得是否已提供全部強制函式名稱。
    /// </summary>
    public bool HasCompleteFunctionSet => MissingFunctions.Count == 0;
}

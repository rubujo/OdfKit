namespace OdfKit.Formula;

/// <summary>
/// Identifies an OASIS OpenFormula evaluator conformance group.
/// 識別 OASIS OpenFormula 評估器一致性群組。
/// </summary>
public enum OdfFormulaConformanceGroup
{
    /// <summary>
    /// The Small Group evaluator requirements.
    /// Small Group 評估器需求。
    /// </summary>
    Small,

    /// <summary>
    /// The cumulative Small and Medium Group evaluator requirements.
    /// 累計 Small 與 Medium Group 評估器需求。
    /// </summary>
    Medium,

    /// <summary>
    /// The cumulative Small, Medium, and Large Group evaluator requirements.
    /// 累計 Small、Medium 與 Large Group 評估器需求。
    /// </summary>
    Large
}

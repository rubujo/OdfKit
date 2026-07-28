namespace OdfKit.Formula;

/// <summary>
/// Identifies why a transactional formula evaluation failed.
/// 識別交易式公式評估失敗的原因。
/// </summary>
public enum OdfFormulaEvaluationFailureReason
{
    /// <summary>
    /// The formula syntax could not be parsed.
    /// 無法剖析公式語法。
    /// </summary>
    InvalidFormula,

    /// <summary>
    /// The formula uses an unsupported function or external capability.
    /// 公式使用不支援的函式或外部能力。
    /// </summary>
    UnsupportedFormula,

    /// <summary>
    /// A configured resource limit was exceeded.
    /// 超過已設定的資源限制。
    /// </summary>
    ResourceLimitExceeded,

    /// <summary>
    /// Trusted application-provided evaluation code failed.
    /// 應用程式提供的受信任評估程式碼失敗。
    /// </summary>
    EvaluationFailed
}

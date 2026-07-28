using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents a transactional spreadsheet formula evaluation failure.
/// 表示交易式試算表公式評估失敗。
/// </summary>
public sealed class OdfFormulaEvaluationException : InvalidOperationException
{
    /// <summary>
    /// Initializes an evaluation exception.
    /// 初始化公式評估例外。
    /// </summary>
    /// <param name="reason">The failure reason. / 失敗原因。</param>
    /// <param name="report">The partial evaluation report. / 部分評估報告。</param>
    /// <param name="message">The localized message. / 在地化訊息。</param>
    public OdfFormulaEvaluationException(
        OdfFormulaEvaluationFailureReason reason,
        OdfFormulaEvaluationReport report,
        string message)
        : base(message)
    {
        Reason = reason;
        Report = report;
    }

    /// <summary>
    /// Gets the failure reason.
    /// 取得失敗原因。
    /// </summary>
    public OdfFormulaEvaluationFailureReason Reason { get; }

    /// <summary>
    /// Gets the partial evaluation report.
    /// 取得部分評估報告。
    /// </summary>
    public OdfFormulaEvaluationReport Report { get; }
}

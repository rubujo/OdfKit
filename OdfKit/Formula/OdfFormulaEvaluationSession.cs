using System;
using System.Threading;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Maintains formula dependency and value snapshots for transactional incremental recalculation.
/// 維護公式相依關係與值快照，以進行交易式增量重算。
/// </summary>
/// <remarks>
/// A session belongs to one spreadsheet document and is not thread-safe. Custom functions,
/// resolvers, and fallbacks configured through the options are caller-trusted code.
/// 工作階段僅屬於一份試算表文件，且不具執行緒安全性。透過選項設定的自訂函式、
/// resolver 與後援皆屬呼叫端信任的程式碼。
/// </remarks>
public sealed class OdfFormulaEvaluationSession
{
    private readonly OdfNode _contentRoot;
    private readonly OdfExternalLinkManager? _externalLinks;
    private readonly OdfFormulaEvaluationOptions _options;
    private readonly DefaultFormulaEvaluator _evaluator;
    private OdfFormulaIncrementalState? _state;

    internal OdfFormulaEvaluationSession(
        OdfNode contentRoot,
        OdfExternalLinkManager? externalLinks,
        OdfFormulaEvaluationOptions options)
    {
        _contentRoot = contentRoot;
        _externalLinks = externalLinks;
        _options = options;
        _evaluator = options.Evaluator ?? new DefaultFormulaEvaluator();
    }

    /// <summary>
    /// Recalculates all formulas on the first call and only affected formulas on later calls.
    /// 第一次呼叫會重算所有公式，後續呼叫只重算受影響的公式。
    /// </summary>
    /// <returns>The transactional evaluation report. / 交易式評估報告。</returns>
    /// <exception cref="OdfFormulaEvaluationException">Thrown when strict evaluation fails. / 當嚴格評估失敗時擲出。</exception>
    public OdfFormulaEvaluationReport Recalculate() =>
        Recalculate(CancellationToken.None);

    /// <summary>
    /// Recalculates affected formulas transactionally with cancellation.
    /// 使用取消權杖，以交易方式重算受影響的公式。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The transactional evaluation report. / 交易式評估報告。</returns>
    /// <exception cref="OdfFormulaEvaluationException">Thrown when strict evaluation fails. / 當嚴格評估失敗時擲出。</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested. / 當要求取消時擲出。</exception>
    public OdfFormulaEvaluationReport Recalculate(CancellationToken cancellationToken)
    {
        (
            OdfFormulaEvaluationReport report,
            OdfFormulaIncrementalState state) =
            FormulaDocumentEvaluationEngine.EvaluateIncrementallyInDocument(
                _contentRoot,
                _evaluator,
                _externalLinks,
                _options,
                _state,
                cancellationToken);
        _state = state;
        return report;
    }

    /// <summary>
    /// Invalidates the retained dependency and value snapshots so the next call performs a full recalculation.
    /// 使保留的相依關係與值快照失效，讓下次呼叫執行完整重算。
    /// </summary>
    public void Invalidate() => _state = null;
}

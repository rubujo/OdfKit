using OdfKit.Formula;

using OdfKit.Compliance;
using OdfKit.Core;

using System;
using System.Threading;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the SpreadsheetDocument API.
/// 提供 SpreadsheetDocument API。
/// </summary>

public partial class SpreadsheetDocument
{
    private OdfExternalLinkManager? _externalLinks;
    private OdfFormulaEvaluationChannel? _formulaEvaluationChannel;

    /// <summary>
    /// Gets the external link manager for cross-document formula references.
    /// 取得跨文件公式引用的外部連結管理器。
    /// </summary>
    public OdfExternalLinkManager ExternalLinks =>
        _externalLinks ??= OdfExternalLinkPersistenceEngine.Load(SettingsDom);

    /// <summary>
    /// Evaluates formulas in the current spreadsheet document and resolves cross-document references with <see cref="ExternalLinks"/>.
    /// 評估目前試算表文件中的公式，並使用 <see cref="ExternalLinks"/> 解析跨文件參照。
    /// </summary>
    public void EvaluateFormulas()
    {
        var evaluator = new DefaultFormulaEvaluator();
        evaluator.EvaluateFormulasInDocument(ContentDom, ExternalLinks);
    }

    /// <summary>
    /// Evaluates formulas with a configured evaluator, including its custom functions and fallback.
    /// 使用已設定的評估器計算公式，包含其自訂函式與後援。
    /// </summary>
    /// <param name="evaluator">The configured formula evaluator. / 已設定的公式評估器。</param>
    public void EvaluateFormulas(DefaultFormulaEvaluator evaluator)
    {
        if (evaluator is null)
        {
            throw new ArgumentNullException(
                nameof(evaluator),
                OdfLocalizer.GetMessage("Err_SpreadsheetDocument_FormulaEvaluatorNull"));
        }

        evaluator.EvaluateFormulasInDocument(ContentDom, ExternalLinks);
    }
    /// <summary>
    /// Short overload of BeginFormulaEvaluationChannel that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：BeginFormulaEvaluationChannel 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfFormulaEvaluationChannel BeginFormulaEvaluationChannel() => BeginFormulaEvaluationChannel(64, default);

    /// <summary>
    /// Short overload of BeginFormulaEvaluationChannel that accepts capacity; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 capacity；其餘可選參數使用預設值並轉呼叫最長 BeginFormulaEvaluationChannel 多載。
    /// </summary>
    public OdfFormulaEvaluationChannel BeginFormulaEvaluationChannel(int capacity) => BeginFormulaEvaluationChannel(capacity, default);


    /// <summary>
    /// Opens an asynchronous formula recalculation channel that queues subsequent cell value or formula changes for background recalculation.
    /// 開啟非同步公式重算通道，將後續儲存格值或公式變更排入背景重算。
    /// </summary>
    /// <param name="capacity">The channel capacity. / 通道容量。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>The started formula recalculation channel. / 已啟動的公式重算通道。</returns>
    public OdfFormulaEvaluationChannel BeginFormulaEvaluationChannel(int capacity, CancellationToken cancellationToken)
    {
        _formulaEvaluationChannel?.Dispose();
        _formulaEvaluationChannel = new OdfFormulaEvaluationChannel(this, capacity, cancellationToken);
        return _formulaEvaluationChannel;
    }


    internal void NotifyFormulaRecalculationRequested()
    {
        _formulaEvaluationChannel?.TryEnqueue();
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非受控資源。
    /// </summary>
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _formulaEvaluationChannel?.Dispose();
            _formulaEvaluationChannel = null;
        }

        base.Dispose(disposing);
    }

    internal override OdfExternalLinkManager? GetFormulaExternalLinksForPersistence() => _externalLinks;

    internal override void PrepareForPersistence(OdfSaveOptions options)
    {
        if (_externalLinks is not null)
        {
            OdfExternalLinkPersistenceEngine.Save(SettingsDom, _externalLinks);
        }
    }
}

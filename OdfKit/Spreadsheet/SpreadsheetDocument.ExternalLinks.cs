using OdfKit.Formula;

using OdfKit.Core;

using System.Threading;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    private OdfExternalLinkManager? _externalLinks;
    private OdfFormulaEvaluationChannel? _formulaEvaluationChannel;

    /// <summary>
    /// 取得跨文件公式引用的外部連結管理器。
    /// </summary>
    public OdfExternalLinkManager ExternalLinks =>
        _externalLinks ??= OdfExternalLinkPersistenceEngine.Load(SettingsDom);

    /// <summary>
    /// 評估目前試算表文件中的公式，並使用 <see cref="ExternalLinks"/> 解析跨文件參照。
    /// </summary>
    public void EvaluateFormulas()
    {
        var evaluator = new DefaultFormulaEvaluator();
        evaluator.EvaluateFormulasInDocument(ContentDom, ExternalLinks);
    }

    /// <summary>
    /// 開啟非同步公式重算通道，將後續儲存格值或公式變更排入背景重算。
    /// </summary>
    /// <param name="capacity">通道容量</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>已啟動的公式重算通道</returns>
    public OdfFormulaEvaluationChannel BeginFormulaEvaluationChannel(int capacity = 64, CancellationToken cancellationToken = default)
    {
        _formulaEvaluationChannel?.Dispose();
        _formulaEvaluationChannel = new OdfFormulaEvaluationChannel(this, capacity, cancellationToken);
        return _formulaEvaluationChannel;
    }

    internal void NotifyFormulaRecalculationRequested()
    {
        _formulaEvaluationChannel?.TryEnqueue();
    }

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

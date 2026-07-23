namespace OdfKit.Formula;

/// <summary>
/// Provides a fallback for formulas that the in-process evaluator cannot calculate.
/// 為處理程序內評估器無法計算的公式提供後援。
/// </summary>
/// <remarks>
/// Implementations may delegate to a remote service or an office-suite calculation engine. They are responsible for isolation, timeouts, and external-data policy.
/// 實作可委派給遠端服務或辦公套件計算引擎，並應自行負責隔離、逾時及外部資料政策。
/// </remarks>
public interface IOdfFormulaEvaluationFallback
{
    /// <summary>
    /// Attempts to evaluate an otherwise unsupported formula.
    /// 嘗試評估原本不受支援的公式。
    /// </summary>
    /// <param name="formula">The normalized formula text. / 標準化後的公式文字。</param>
    /// <param name="context">The spreadsheet evaluation context. / 試算表評估內容模型。</param>
    /// <param name="result">The result when evaluation succeeds. / 評估成功時的結果。</param>
    /// <returns><see langword="true"/> when a result was produced. / 若已產生結果則為 <see langword="true"/>。</returns>
    bool TryEvaluate(string formula, IEvaluationContext context, out object result);
}

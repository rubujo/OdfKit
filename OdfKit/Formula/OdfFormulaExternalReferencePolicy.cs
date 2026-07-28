namespace OdfKit.Formula;

/// <summary>
/// Controls access to external spreadsheet references during formula evaluation.
/// 控制公式評估期間對外部試算表參照的存取方式。
/// </summary>
public enum OdfFormulaExternalReferencePolicy
{
    /// <summary>
    /// Uses only values already present in the external-reference cache.
    /// 僅使用外部參照快取中既有的值。
    /// </summary>
    CachedOnly,

    /// <summary>
    /// Allows the explicitly configured document resolver to obtain missing values.
    /// 允許明確設定的文件解析器取得缺少的值。
    /// </summary>
    AllowConfiguredResolver
}

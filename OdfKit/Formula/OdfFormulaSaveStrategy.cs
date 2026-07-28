namespace OdfKit.Formula;

/// <summary>
/// Specifies how spreadsheet formula results are handled while saving.
/// 指定儲存時如何處理試算表公式結果。
/// </summary>
public enum OdfFormulaSaveStrategy
{
    /// <summary>
    /// Preserves formulas and their cached results without recalculation.
    /// 保留公式及其快取結果，不進行重新計算。
    /// </summary>
    PreserveCachedValues,

    /// <summary>
    /// Preserves formulas, clears cached results, and requests recalculation by the consumer.
    /// 保留公式、清除快取結果，並要求消費端重新計算。
    /// </summary>
    MarkForRecalculation,

    /// <summary>
    /// Calculates supported formulas transactionally before saving.
    /// 儲存前以交易方式計算支援的公式。
    /// </summary>
    Calculate
}

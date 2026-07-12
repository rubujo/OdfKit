using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Reports the result of a high-level spreadsheet range write operation.
/// 回報高階試算表範圍寫入作業的結果。
/// </summary>
public sealed class OdfRangeWriteReport
{
    /// <summary>
    /// Gets or sets the updated range.
    /// 取得或設定已更新範圍。
    /// </summary>
    public OdfCellRange Range { get; set; }

    /// <summary>
    /// Gets or sets the number of cells whose values were written.
    /// 取得或設定已寫入值的儲存格數量。
    /// </summary>
    public int WrittenCellCount { get; set; }

    /// <summary>
    /// Gets or sets the number of trailing cells cleared by the operation.
    /// 取得或設定作業清除的尾端儲存格數量。
    /// </summary>
    public int ClearedCellCount { get; set; }

    /// <summary>
    /// Gets or sets the number of cells skipped by the operation.
    /// 取得或設定作業略過的儲存格數量。
    /// </summary>
    public int SkippedCellCount { get; set; }

    /// <summary>
    /// Gets warnings produced by the write operation.
    /// 取得寫入作業產生的警告。
    /// </summary>
    public IList<string> Warnings { get; } = new List<string>();

    /// <summary>
    /// Gets <see cref="Warnings"/> as strongly typed diagnostics.
    /// 取得 <see cref="Warnings"/> 的強型別診斷檢視。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        OdfDiagnostic.FromStrings(Warnings, "Warning", OdfIssueSeverity.Warning);
}

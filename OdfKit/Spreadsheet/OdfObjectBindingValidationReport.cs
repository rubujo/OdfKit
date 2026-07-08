using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Reports object binding validation results.
/// 回報物件繫結驗證結果。
/// </summary>
public sealed class OdfObjectBindingValidationReport
{
    /// <summary>
    /// Gets or sets the validated range.
    /// 取得或設定已驗證的範圍。
    /// </summary>
    public OdfCellRange Range { get; set; }

    /// <summary>
    /// Gets validation diagnostics.
    /// 取得驗證診斷資訊。
    /// </summary>
    public IList<OdfObjectBindingDiagnostic> Diagnostics { get; } = new List<OdfObjectBindingDiagnostic>();

    /// <summary>
    /// Gets a value indicating whether validation found errors.
    /// 取得驗證是否找到錯誤。
    /// </summary>
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity >= OdfIssueSeverity.Error);

    /// <summary>
    /// Gets a value indicating whether validation found warnings.
    /// 取得驗證是否找到警告。
    /// </summary>
    public bool HasWarnings => Diagnostics.Any(diagnostic => diagnostic.Severity == OdfIssueSeverity.Warning);

    /// <summary>
    /// Gets the number of error diagnostics.
    /// 取得錯誤診斷數量。
    /// </summary>
    public int ErrorCount => Diagnostics.Count(diagnostic => diagnostic.Severity >= OdfIssueSeverity.Error);

    /// <summary>
    /// Gets the number of warning diagnostics.
    /// 取得警告診斷數量。
    /// </summary>
    public int WarningCount => Diagnostics.Count(diagnostic => diagnostic.Severity == OdfIssueSeverity.Warning);
}

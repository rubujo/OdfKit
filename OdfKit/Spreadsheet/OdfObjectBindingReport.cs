using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Reports the result of object binding operations.
/// 回報物件繫結操作的結果。
/// </summary>
public sealed class OdfObjectBindingReport
{
    /// <summary>
    /// Gets or sets the affected cell range.
    /// 取得或設定受影響的儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; set; }

    /// <summary>
    /// Gets or sets the number of data rows processed.
    /// 取得或設定已處理的資料列數。
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Gets or sets the number of mapped columns.
    /// 取得或設定已對應的欄位數。
    /// </summary>
    public int ColumnCount { get; set; }

    /// <summary>
    /// Gets the resolved column names.
    /// 取得已解析的欄位名稱。
    /// </summary>
    public IList<string> ColumnNames { get; } = new List<string>();

    /// <summary>
    /// Gets properties or columns skipped during binding.
    /// 取得繫結期間略過的屬性或欄位。
    /// </summary>
    public IList<string> SkippedColumns { get; } = new List<string>();

    /// <summary>
    /// Gets non-fatal binding warnings.
    /// 取得非致命的繫結警告。
    /// </summary>
    public IList<string> Warnings { get; } = new List<string>();

    /// <summary>
    /// Gets structured diagnostics produced during binding.
    /// 取得繫結期間產生的結構化診斷資訊。
    /// </summary>
    public IList<OdfObjectBindingDiagnostic> Diagnostics { get; } = new List<OdfObjectBindingDiagnostic>();

    /// <summary>
    /// Gets or sets the inserted row count.
    /// 取得或設定已新增列數。
    /// </summary>
    public int InsertedRowCount { get; set; }

    /// <summary>
    /// Gets or sets the updated row count.
    /// 取得或設定已更新列數。
    /// </summary>
    public int UpdatedRowCount { get; set; }

    /// <summary>
    /// Gets or sets the skipped row count.
    /// 取得或設定已略過列數。
    /// </summary>
    public int SkippedRowCount { get; set; }

    /// <summary>
    /// Gets or sets the deleted row count.
    /// 取得或設定已刪除列數。
    /// </summary>
    public int DeletedRowCount { get; set; }

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

    /// <summary>
    /// Gets a value indicating whether the report contains errors.
    /// 取得報告是否包含錯誤。
    /// </summary>
    public bool HasErrors => ErrorCount > 0;

    /// <summary>
    /// Gets a value indicating whether the report contains warnings.
    /// 取得報告是否包含警告。
    /// </summary>
    public bool HasWarnings => WarningCount > 0;

    /// <summary>
    /// Gets the number of rows affected by insert, update, or delete operations.
    /// 取得新增、更新或刪除操作影響的列數。
    /// </summary>
    public int AffectedRowCount => InsertedRowCount + UpdatedRowCount + DeletedRowCount;

    /// <summary>
    /// Gets a value indicating whether the report contains errors or warnings.
    /// 取得報告是否包含錯誤或警告。
    /// </summary>
    public bool HasIssues => HasErrors || HasWarnings;
}

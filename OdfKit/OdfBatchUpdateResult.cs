using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit;

/// <summary>
/// Represents the result of a high-level batch update operation.
/// 表示高階批次更新作業的結果。
/// </summary>
public sealed class OdfBatchUpdateResult
{
    /// <summary>
    /// Gets or sets the number of items updated successfully.
    /// 取得或設定成功更新的項目數量。
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Gets the names or identifiers that were requested but not found.
    /// 取得要求更新但未找到的名稱或識別碼。
    /// </summary>
    public IList<string> MissingNames { get; } = new List<string>();

    /// <summary>
    /// Gets the names or identifiers updated successfully.
    /// 取得成功更新的名稱或識別碼。
    /// </summary>
    public IList<string> UpdatedNames { get; } = new List<string>();

    /// <summary>
    /// Gets the names or identifiers that matched more than one item.
    /// 取得符合多個項目的名稱或識別碼。
    /// </summary>
    public IList<string> AmbiguousNames { get; } = new List<string>();

    /// <summary>
    /// Gets the names or identifiers that were found but did not require changes.
    /// 取得找到但不需變更的名稱或識別碼。
    /// </summary>
    public IList<string> UnchangedNames { get; } = new List<string>();

    /// <summary>
    /// Gets the non-fatal warnings produced while applying the update.
    /// 取得套用更新時產生的非致命警告。
    /// </summary>
    public IList<string> Warnings { get; } = new List<string>();

    /// <summary>
    /// Gets missing, ambiguous, and warning entries as strongly typed diagnostics.
    /// 取得找不到、語意不明與警告項目的強型別診斷。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        OdfDiagnostic.FromStrings(MissingNames, "MissingName", OdfIssueSeverity.Error)
            .Concat(OdfDiagnostic.FromStrings(AmbiguousNames, "AmbiguousName", OdfIssueSeverity.Error))
            .Concat(OdfDiagnostic.FromStrings(Warnings, "Warning", OdfIssueSeverity.Warning))
            .ToList();
}

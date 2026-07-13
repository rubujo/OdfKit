using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Drawing;

/// <summary>
/// Reports the outcome of a drawing shape layout operation.
/// 回報繪圖圖形版面配置作業的結果。
/// </summary>
public sealed class OdfShapeLayoutResult
{
    /// <summary>
    /// Gets the identifiers of shapes whose positions changed.
    /// 取得位置已變更的圖形識別碼。
    /// </summary>
    public IList<string> UpdatedShapeIds { get; } = new List<string>();

    /// <summary>
    /// Gets the requested identifiers that were not found.
    /// 取得要求處理但找不到的識別碼。
    /// </summary>
    public IList<string> MissingShapeIds { get; } = new List<string>();

    /// <summary>
    /// Gets the identifiers skipped because their geometry was incomplete.
    /// 取得因幾何資訊不完整而略過的識別碼。
    /// </summary>
    public IList<string> InvalidGeometryShapeIds { get; } = new List<string>();

    /// <summary>
    /// Gets the number of shapes whose positions changed.
    /// 取得位置已變更的圖形數量。
    /// </summary>
    public int UpdatedCount => UpdatedShapeIds.Count;

    /// <summary>
    /// Gets missing-shape and invalid-geometry entries as strongly typed diagnostics.
    /// 取得找不到圖形與無效幾何資訊的強型別診斷。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        OdfDiagnostic.FromStrings(MissingShapeIds, "MissingShapeId", OdfIssueSeverity.Error)
            .Concat(OdfDiagnostic.FromStrings(InvalidGeometryShapeIds, "InvalidGeometryShapeId", OdfIssueSeverity.Error))
            .ToList();
}

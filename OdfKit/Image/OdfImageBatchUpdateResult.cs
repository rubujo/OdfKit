using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Image;

/// <summary>
/// Reports the result of a batch image frame update.
/// 回報批次影像框架更新作業的結果。
/// </summary>
public sealed class OdfImageBatchUpdateResult
{
    /// <summary>
    /// Gets or sets the number of updated frames.
    /// 取得或設定已更新框架數。
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Gets frame names that were requested but not found.
    /// 取得要求更新但找不到的框架名稱。
    /// </summary>
    public IList<string> MissingNames { get; } = new List<string>();

    /// <summary>
    /// Gets <see cref="MissingNames"/> as strongly typed diagnostics.
    /// 取得 <see cref="MissingNames"/> 的強型別診斷檢視。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        OdfDiagnostic.FromStrings(MissingNames, "MissingName", OdfIssueSeverity.Error);
}

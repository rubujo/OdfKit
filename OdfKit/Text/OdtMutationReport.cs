using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Text;

/// <summary>
/// Reports a task-oriented ODT mutation.
/// 回報任務導向 ODT 變更作業。
/// </summary>
/// <param name="operation">The domain operation name. / 領域作業名稱。</param>
public sealed class OdtMutationReport(string operation)
{
    /// <summary>
    /// Gets the domain operation name.
    /// 取得領域作業名稱。
    /// </summary>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets or sets the number of updated domain objects.
    /// 取得或設定已更新的領域物件數量。
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Gets requested targets that were not found.
    /// 取得要求處理但找不到的目標。
    /// </summary>
    public IList<string> MissingTargets { get; } = new List<string>();

    /// <summary>
    /// Gets targets that matched more than one domain object.
    /// 取得符合多個領域物件的目標。
    /// </summary>
    public IList<string> AmbiguousTargets { get; } = new List<string>();

    /// <summary>
    /// Gets package paths created by the operation.
    /// 取得作業所建立的封裝路徑。
    /// </summary>
    public IList<string> CreatedPackagePaths { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether the operation changed the document.
    /// 取得作業是否已變更文件。
    /// </summary>
    public bool Changed => UpdatedCount > 0;

    /// <summary>
    /// Gets missing and ambiguous targets as strongly typed diagnostics.
    /// 取得找不到與語意不明目標的強型別診斷。
    /// </summary>
    public IReadOnlyList<OdfDiagnostic> Diagnostics =>
        OdfDiagnostic.FromStrings(MissingTargets, "MissingTarget", OdfIssueSeverity.Error)
            .Concat(OdfDiagnostic.FromStrings(AmbiguousTargets, "AmbiguousTarget", OdfIssueSeverity.Error))
            .ToList();
}

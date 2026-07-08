using System.Collections.Generic;

namespace OdfKit.Image;

/// <summary>
/// Reports practical image inspection results.
/// 回報實務圖片檢查結果。
/// </summary>
public sealed class OdfImageInspectionReport
{
    /// <summary>
    /// Gets the inspection issues.
    /// 取得檢查問題清單。
    /// </summary>
    public IList<OdfImageInspectionIssue> Issues { get; } = new List<OdfImageInspectionIssue>();

    /// <summary>
    /// Gets a value indicating whether no issues were found.
    /// 取得是否未發現任何問題。
    /// </summary>
    public bool IsPortable => Issues.Count == 0;
}

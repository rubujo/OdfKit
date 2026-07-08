using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// Configures post-processing for practical compatibility validation.
/// 設定實務相容性驗證的後處理行為。
/// </summary>
public sealed class OdfPracticalCompatibilityOptions
{
    /// <summary>
    /// Gets rule identifiers that should be suppressed.
    /// 取得要停用的規則識別碼。
    /// </summary>
    public ISet<string> DisabledRuleIds { get; } = new HashSet<string>(System.StringComparer.Ordinal);

    /// <summary>
    /// Gets severity overrides keyed by rule identifier.
    /// 取得依規則識別碼覆寫的嚴重性。
    /// </summary>
    public IDictionary<string, OdfIssueSeverity> SeverityOverrides { get; } =
        new Dictionary<string, OdfIssueSeverity>(System.StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the maximum number of issues returned.
    /// 取得或設定最多回傳的問題數量。
    /// </summary>
    public int? MaximumIssueCount { get; set; }
}

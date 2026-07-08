using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// Represents the result of a practical compatibility validation pass.
/// 表示實務相容性驗證程序的結果。
/// </summary>
public sealed class OdfPracticalCompatibilityReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfPracticalCompatibilityReport"/> class.
    /// 初始化 <see cref="OdfPracticalCompatibilityReport"/> 類別的新執行個體。
    /// </summary>
    /// <param name="profile">The profile used for validation. / 驗證使用的設定檔。</param>
    /// <param name="documentKind">The document kind. / 文件種類。</param>
    /// <param name="issues">The reported issues. / 回報的問題。</param>
    public OdfPracticalCompatibilityReport(
        OdfPracticalCompatibilityProfile profile,
        OdfDocumentKind documentKind,
        IEnumerable<OdfPracticalCompatibilityIssue> issues)
    {
        Profile = profile;
        DocumentKind = documentKind;
        Issues = issues?.ToArray() ?? [];
    }

    /// <summary>
    /// Gets the profile used for validation.
    /// 取得驗證使用的設定檔。
    /// </summary>
    public OdfPracticalCompatibilityProfile Profile { get; }

    /// <summary>
    /// Gets the document kind.
    /// 取得文件種類。
    /// </summary>
    public OdfDocumentKind DocumentKind { get; }

    /// <summary>
    /// Gets the reported practical compatibility issues.
    /// 取得回報的實務相容性問題。
    /// </summary>
    public IReadOnlyList<OdfPracticalCompatibilityIssue> Issues { get; }

    /// <summary>
    /// Gets whether the report contains no warnings or errors.
    /// 取得報告是否不含警告或錯誤。
    /// </summary>
    public bool IsCompatible => Issues.All(issue => issue.Severity < OdfIssueSeverity.Warning);
}

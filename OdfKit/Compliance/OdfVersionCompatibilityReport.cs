using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// Identifies the kind of ODF version compatibility loss.
/// 識別 ODF 版本相容性損失的種類。
/// </summary>
public enum OdfVersionCompatibilityIssueKind
{
    /// <summary>
    /// Indicates that an element is unavailable in the target ODF version.
    /// 指出某個元素無法由目標 ODF 版本表示。
    /// </summary>
    ElementNotSupported,

    /// <summary>
    /// Indicates that an attribute is unavailable in the target ODF version.
    /// 指出某個屬性無法由目標 ODF 版本表示。
    /// </summary>
    AttributeNotSupported
}

/// <summary>
/// Describes one semantic construct that cannot be represented by a target ODF version.
/// 描述一個無法由目標 ODF 版本表示的語意結構。
/// </summary>
/// <param name="Kind">The issue kind. / 問題種類。</param>
/// <param name="NamespaceUri">The namespace URI. / 命名空間 URI。</param>
/// <param name="LocalName">The local name. / 區域名稱。</param>
/// <param name="Path">The package entry and DOM path. / 封裝項目與 DOM 路徑。</param>
/// <param name="SourceVersion">The source semantic model version. / 來源語意模型版本。</param>
/// <param name="TargetVersion">The requested target version. / 要求的目標版本。</param>
public sealed record OdfVersionCompatibilityIssue(
    OdfVersionCompatibilityIssueKind Kind,
    string NamespaceUri,
    string LocalName,
    string Path,
    OdfVersion SourceVersion,
    OdfVersion TargetVersion);

/// <summary>
/// Reports whether a document can be represented by a target ODF version without semantic loss.
/// 回報文件是否能在不損失語意的情況下由目標 ODF 版本表示。
/// </summary>
public sealed class OdfVersionCompatibilityReport
{
    internal OdfVersionCompatibilityReport(
        OdfVersion sourceVersion,
        OdfVersion targetVersion,
        IReadOnlyList<OdfVersionCompatibilityIssue> issues)
    {
        SourceVersion = sourceVersion;
        TargetVersion = targetVersion;
        Issues = issues;
    }

    /// <summary>
    /// Gets the semantic model version used for analysis.
    /// 取得分析時使用的語意模型版本。
    /// </summary>
    public OdfVersion SourceVersion { get; }

    /// <summary>
    /// Gets the requested target ODF version.
    /// 取得要求的目標 ODF 版本。
    /// </summary>
    public OdfVersion TargetVersion { get; }

    /// <summary>
    /// Gets issues that cannot be represented by the target version.
    /// 取得無法由目標版本表示的問題。
    /// </summary>
    public IReadOnlyList<OdfVersionCompatibilityIssue> Issues { get; }

    /// <summary>
    /// Gets a value indicating whether the requested version conversion is semantically safe.
    /// 取得一個值，指出要求的版本轉換是否不會損失語意。
    /// </summary>
    public bool IsSafe => Issues.Count == 0;
}

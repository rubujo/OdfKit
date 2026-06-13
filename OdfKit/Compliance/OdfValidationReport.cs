#pragma warning restore CS1591
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 代表 ODF 套件或文件的驗證結果。
/// </summary>
/// <param name="detectedVersion">偵測到的 ODF 版本</param>
/// <param name="documentKind">偵測到的 ODF 文件種類</param>
/// <param name="issues">驗證問題集合</param>
public sealed class OdfValidationReport(OdfVersion detectedVersion, OdfDocumentKind documentKind, IEnumerable<OdfValidationIssue> issues)
{
    /// <summary>
    /// 取得一個值，表示驗證的文件是否符合選取的檢查項目。
    /// </summary>
    public bool IsValid { get; } = !new List<OdfValidationIssue>(issues ?? []).Any(issue => issue.Severity is OdfIssueSeverity.Error or OdfIssueSeverity.Fatal);

    /// <summary>
    /// 取得偵測到的 ODF 版本。
    /// </summary>
    public OdfVersion DetectedVersion { get; } = detectedVersion;

    /// <summary>
    /// 取得偵測到的 ODF 文件種類。
    /// </summary>
    public OdfDocumentKind DocumentKind { get; } = documentKind;

    /// <summary>
    /// 取得所有驗證問題。
    /// </summary>
    public IReadOnlyList<OdfValidationIssue> Issues { get; } = new List<OdfValidationIssue>(issues ?? []).AsReadOnly();
}

/// <summary>
/// 代表單一驗證問題。
/// </summary>
/// <param name="severity">問題嚴重性</param>
/// <param name="ruleId">規則識別碼</param>
/// <param name="message">人類可讀的問題說明訊息</param>
/// <param name="packagePath">與此問題相關的套件進入路徑</param>
/// <param name="xPath">與此問題相關的 XML 路徑（可用時）</param>
/// <param name="requiredVersion">問題與版本相關時所需的 ODF 版本</param>
/// <param name="profileId">發出此問題的相容性設定檔識別碼</param>
public sealed class OdfValidationIssue(
    OdfIssueSeverity severity,
    string ruleId,
    string message,
    string? packagePath = null,
    string? xPath = null,
    OdfVersion? requiredVersion = null,
    string? profileId = null)
{
    /// <summary>
    /// 取得問題嚴重性。
    /// </summary>
    public OdfIssueSeverity Severity { get; } = severity;

    /// <summary>
    /// 取得規則識別碼。
    /// </summary>
    public string RuleId { get; } = ruleId ?? string.Empty;

    /// <summary>
    /// 取得人類可讀的問題說明訊息。
    /// </summary>
    public string Message { get; } = message ?? string.Empty;

    /// <summary>
    /// 取得與此問題相關的套件進入路徑。
    /// </summary>
    public string? PackagePath { get; } = packagePath;

    /// <summary>
    /// 取得與此問題相關的 XML 路徑（可用時）。
    /// </summary>
    public string? XPath { get; } = xPath;

    /// <summary>
    /// 取得問題與版本相關時所需的 ODF 版本。
    /// </summary>
    public OdfVersion? RequiredVersion { get; } = requiredVersion;

    /// <summary>
    /// 取得發出此問題的相容性設定檔識別碼。
    /// </summary>
    public string? ProfileId { get; } = profileId;
}

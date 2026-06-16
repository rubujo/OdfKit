using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 表示單一驗證問題的 JSON 匯出模型。
/// </summary>
public sealed class OdfValidationIssueJsonModel
{
    /// <summary>
    /// 初始化 <see cref="OdfValidationIssueJsonModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="severity">問題嚴重性。</param>
    /// <param name="ruleId">規則識別碼。</param>
    /// <param name="message">問題說明訊息。</param>
    /// <param name="packagePath">套件路徑。</param>
    /// <param name="xPath">XML 位置。</param>
    /// <param name="requiredVersion">所需 ODF 版本。</param>
    /// <param name="profileId">相容性設定檔識別碼。</param>
    /// <param name="suggestedFix">建議修復文字。</param>
    /// <param name="details">結構化診斷細節。</param>
    public OdfValidationIssueJsonModel(
        string severity,
        string ruleId,
        string message,
        string? packagePath,
        string? xPath,
        string? requiredVersion,
        string? profileId,
        string suggestedFix,
        IReadOnlyDictionary<string, string?>? details)
    {
        Severity = severity ?? string.Empty;
        RuleId = ruleId ?? string.Empty;
        Message = message ?? string.Empty;
        PackagePath = packagePath;
        XPath = xPath;
        RequiredVersion = requiredVersion;
        ProfileId = profileId;
        SuggestedFix = suggestedFix ?? string.Empty;
        Details = OdfValidationReport.CopyDetails(details);
    }

    /// <summary>
    /// 取得問題嚴重性。
    /// </summary>
    public string Severity { get; }

    /// <summary>
    /// 取得規則識別碼。
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    /// 取得問題說明訊息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 取得套件路徑。
    /// </summary>
    public string? PackagePath { get; }

    /// <summary>
    /// 取得 XML 位置。
    /// </summary>
    public string? XPath { get; }

    /// <summary>
    /// 取得所需 ODF 版本。
    /// </summary>
    public string? RequiredVersion { get; }

    /// <summary>
    /// 取得相容性設定檔識別碼。
    /// </summary>
    public string? ProfileId { get; }

    /// <summary>
    /// 取得建議修復文字。
    /// </summary>
    public string SuggestedFix { get; }

    /// <summary>
    /// 取得結構化診斷細節。
    /// </summary>
    public IReadOnlyDictionary<string, string?> Details { get; }
}

using System.Globalization;
using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// Provides the OdfValidationIssue API.
/// 代表單一驗證問題。
/// </summary>
/// <param name="severity">問題嚴重性</param>
/// <param name="ruleId">規則識別碼</param>
/// <param name="message">人類可讀的問題說明訊息</param>
/// <param name="packagePath">與此問題相關的套件進入路徑</param>
/// <param name="xPath">與此問題相關的 XML 路徑（可用時）</param>
/// <param name="requiredVersion">問題與版本相關時所需的 ODF 版本</param>
/// <param name="profileId">發出此問題的相容性設定檔識別碼</param>
/// <param name="details">可供工具處理的結構化診斷細節</param>
/// <param name="culture">The culture used when creating the issue, for SuggestedFix localization. / 產生此問題時使用的文化特性，用於 SuggestedFix 的在地化。</param>
public sealed class OdfValidationIssue(
    OdfIssueSeverity severity,
    string ruleId,
    string message,
    string? packagePath = null,
    string? xPath = null,
    OdfVersion? requiredVersion = null,
    string? profileId = null,
    IReadOnlyDictionary<string, string?>? details = null,
    CultureInfo? culture = null)
{
    /// <summary>
    /// Gets the Severity value.
    /// 取得問題嚴重性。
    /// </summary>
    public OdfIssueSeverity Severity { get; } = severity;

    /// <summary>
    /// Gets the RuleId value.
    /// 取得規則識別碼。
    /// </summary>
    public string RuleId { get; } = ruleId ?? string.Empty;

    /// <summary>
    /// Gets the Message value.
    /// 取得人類可讀的問題說明訊息。
    /// </summary>
    public string Message { get; } = message ?? string.Empty;

    /// <summary>
    /// Gets the PackagePath value.
    /// 取得與此問題相關的套件進入路徑。
    /// </summary>
    public string? PackagePath { get; } = packagePath;

    /// <summary>
    /// Gets the XPath value.
    /// 取得與此問題相關的 XML 路徑（可用時）。
    /// </summary>
    public string? XPath { get; } = xPath;

    /// <summary>
    /// Gets the RequiredVersion value.
    /// 取得問題與版本相關時所需的 ODF 版本。
    /// </summary>
    public OdfVersion? RequiredVersion { get; } = requiredVersion;

    /// <summary>
    /// Gets the ProfileId value.
    /// 取得發出此問題的相容性設定檔識別碼。
    /// </summary>
    public string? ProfileId { get; } = profileId;

    /// <summary>
    /// Gets the Culture value.
    /// 取得與此問題相關的文化特性，用於本地化翻譯。
    /// </summary>
    public CultureInfo? Culture { get; set; } = culture;

    /// <summary>
    /// Gets the Details value.
    /// 取得可供工具處理的結構化診斷細節。
    /// </summary>
    public IReadOnlyDictionary<string, string?> Details { get; } = OdfValidationReport.CopyDetails(details);

    /// <summary>
    /// Performs suggested fix.
    /// 取得使用者可採取的建議修復文字。
    /// </summary>
    public string SuggestedFix => BuildSuggestedFix();

    /// <summary>
    /// Converts to json model.
    /// 建立可序列化的 JSON 匯出模型。
    /// </summary>
    /// <returns>包含驗證問題欄位的 JSON 匯出模型</returns>
    public OdfValidationIssueJsonModel ToJsonModel()
    {
        return new OdfValidationIssueJsonModel(
            Severity.ToString(),
            RuleId,
            Message,
            PackagePath,
            XPath,
            RequiredVersion?.ToString(),
            ProfileId,
            SuggestedFix,
            Details);
    }

    private string BuildSuggestedFix()
    {
        string fix = OdfLocalizer.GetSuggestedFix(RuleId, Culture);
        if (RuleId == "ODF0400" || RuleId == "ODF1002")
        {
            string defaultLoc = "相關 XML 節點";
            var current = Culture ?? CultureInfo.CurrentUICulture;
            if (current is not null && !current.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                defaultLoc = "relevant XML node";
            }
            string location = PackagePath ?? defaultLoc;
            try
            {
                return string.Format(fix, location);
            }
            catch (FormatException)
            {
                return fix;
            }
        }
        return fix;
    }
}

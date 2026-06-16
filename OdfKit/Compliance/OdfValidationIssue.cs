using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

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
/// <param name="details">可供工具處理的結構化診斷細節。</param>
public sealed class OdfValidationIssue(
    OdfIssueSeverity severity,
    string ruleId,
    string message,
    string? packagePath = null,
    string? xPath = null,
    OdfVersion? requiredVersion = null,
    string? profileId = null,
    IReadOnlyDictionary<string, string?>? details = null)
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

    /// <summary>
    /// 取得可供工具處理的結構化診斷細節。
    /// </summary>
    public IReadOnlyDictionary<string, string?> Details { get; } = OdfValidationReport.CopyDetails(details);

    /// <summary>
    /// 取得使用者可採取的建議修復文字。
    /// </summary>
    public string SuggestedFix => BuildSuggestedFix();

    /// <summary>
    /// 建立可序列化的 JSON 匯出模型。
    /// </summary>
    /// <returns>包含驗證問題欄位的 JSON 匯出模型。</returns>
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
        string location = PackagePath ?? "相關 XML 節點";
        return RuleId switch
        {
            "ODF0001" => "加入正確的 mimetype entry。",
            "ODF0003" => "將 mimetype entry 放在 ZIP 封裝第一個項目。",
            "ODF0004" => "將 mimetype entry 設為未壓縮儲存。",
            "ODF0100" => "加入 META-INF/manifest.xml 並描述封裝內容。",
            "ODF0200" or "ODF0201" => "移除不安全或不符合 ODF 路徑規則的 ZIP entry。",
            "ODF0300" or "ODF3000" or "ODF3100" or "ODF3101" or "ODF3102" => "依 ODF 1.4 schema 修正 XML 結構。",
            "ODF0400" or "ODF1002" => "加入或修正 " + location + " 的 office:version。",
            "ODF0500" or "ODF0501" or "ODF2006" or "ODF3002" => "修正 office:body 內的文件種類，使其與 MIME 類型和副檔名一致。",
            "ODF1000" or "ODF1001" => "確認文件版本與選取的相容性設定檔一致。",
            "DisallowInvalidOdfNamespaceExtensions" => "移除或更正 ODF 命名空間中未被 schema 定義的元素或屬性。",
            "RequireForeignExtensionIsolation" => "將擴充內容放在非 ODF 命名空間，並確保可安全移除。",
            "DisallowMacroByDefault" => "移除巨集、指令碼與相關事件監聽器，或改用允許巨集的政策設定。",
            "RequireSafeExternalResourcePolicy" => "改用內嵌資源或確認外部連結符合部署政策。",
            _ => "依驗證訊息修正文件內容。"
        };
    }
}

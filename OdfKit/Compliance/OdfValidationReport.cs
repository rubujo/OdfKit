#pragma warning restore CS1591
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OdfKit.Compliance;

/// <summary>
/// 代表 ODF 套件或文件的驗證結果。
/// </summary>
/// <param name="detectedVersion">偵測到的 ODF 版本</param>
/// <param name="documentKind">偵測到的 ODF 文件種類</param>
/// <param name="issues">驗證問題集合</param>
public sealed class OdfValidationReport(OdfVersion detectedVersion, OdfDocumentKind documentKind, IEnumerable<OdfValidationIssue> issues)
{
    private readonly IReadOnlyList<OdfValidationIssue> _issues = new List<OdfValidationIssue>(issues ?? []).AsReadOnly();

    /// <summary>
    /// 取得一個值，表示驗證的文件是否符合選取的檢查項目。
    /// </summary>
    public bool IsValid => BlockingIssueCount == 0;

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
    public IReadOnlyList<OdfValidationIssue> Issues => _issues;

    /// <summary>
    /// 取得資訊性問題數量。
    /// </summary>
    public int InfoCount => CountSeverity(OdfIssueSeverity.Info);

    /// <summary>
    /// 取得警告問題數量。
    /// </summary>
    public int WarningCount => CountSeverity(OdfIssueSeverity.Warning);

    /// <summary>
    /// 取得錯誤問題數量。
    /// </summary>
    public int ErrorCount => CountSeverity(OdfIssueSeverity.Error);

    /// <summary>
    /// 取得致命問題數量。
    /// </summary>
    public int FatalCount => CountSeverity(OdfIssueSeverity.Fatal);

    /// <summary>
    /// 取得會讓驗證失敗的問題數量。
    /// </summary>
    public int BlockingIssueCount => ErrorCount + FatalCount;

    /// <summary>
    /// 取得依嚴重性彙整的問題數量。
    /// </summary>
    public IReadOnlyDictionary<OdfIssueSeverity, int> IssuesBySeverity => new Dictionary<OdfIssueSeverity, int>
    {
        [OdfIssueSeverity.Info] = InfoCount,
        [OdfIssueSeverity.Warning] = WarningCount,
        [OdfIssueSeverity.Error] = ErrorCount,
        [OdfIssueSeverity.Fatal] = FatalCount
    };

    /// <summary>
    /// 建立可序列化的 JSON 匯出模型。
    /// </summary>
    /// <returns>包含驗證報告摘要與問題清單的 JSON 匯出模型。</returns>
    public OdfValidationReportJsonModel ToJsonModel()
    {
        return new OdfValidationReportJsonModel(
            IsValid,
            DetectedVersion.ToString(),
            DocumentKind.ToString(),
            InfoCount,
            WarningCount,
            ErrorCount,
            FatalCount,
            BlockingIssueCount,
            Issues.Select(issue => issue.ToJsonModel()).ToArray());
    }

    /// <summary>
    /// 將驗證報告匯出為穩定 JSON 字串。
    /// </summary>
    /// <returns>JSON 格式的驗證報告。</returns>
    public string ToJson()
    {
        OdfValidationReportJsonModel model = ToJsonModel();
        var builder = new StringBuilder();
        builder.Append('{');
        AppendJsonProperty(builder, "isValid", model.IsValid);
        builder.Append(',');
        AppendJsonProperty(builder, "detectedVersion", model.DetectedVersion);
        builder.Append(',');
        AppendJsonProperty(builder, "documentKind", model.DocumentKind);
        builder.Append(',');
        AppendJsonProperty(builder, "infoCount", model.InfoCount);
        builder.Append(',');
        AppendJsonProperty(builder, "warningCount", model.WarningCount);
        builder.Append(',');
        AppendJsonProperty(builder, "errorCount", model.ErrorCount);
        builder.Append(',');
        AppendJsonProperty(builder, "fatalCount", model.FatalCount);
        builder.Append(',');
        AppendJsonProperty(builder, "blockingIssueCount", model.BlockingIssueCount);
        builder.Append(',');
        builder.Append("\"issues\":[");
        for (int i = 0; i < model.Issues.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendIssueJson(builder, model.Issues[i]);
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private int CountSeverity(OdfIssueSeverity severity)
    {
        return _issues.Count(issue => issue.Severity == severity);
    }

    private static void AppendIssueJson(StringBuilder builder, OdfValidationIssueJsonModel issue)
    {
        builder.Append('{');
        AppendJsonProperty(builder, "severity", issue.Severity);
        builder.Append(',');
        AppendJsonProperty(builder, "ruleId", issue.RuleId);
        builder.Append(',');
        AppendJsonProperty(builder, "message", issue.Message);
        builder.Append(',');
        AppendJsonProperty(builder, "packagePath", issue.PackagePath);
        builder.Append(',');
        AppendJsonProperty(builder, "xPath", issue.XPath);
        builder.Append(',');
        AppendJsonProperty(builder, "requiredVersion", issue.RequiredVersion);
        builder.Append(',');
        AppendJsonProperty(builder, "profileId", issue.ProfileId);
        builder.Append(',');
        AppendJsonProperty(builder, "suggestedFix", issue.SuggestedFix);
        builder.Append('}');
    }

    private static void AppendJsonProperty(StringBuilder builder, string name, string? value)
    {
        builder.Append('"').Append(EscapeJson(name)).Append("\":");
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        builder.Append('"').Append(EscapeJson(value)).Append('"');
    }

    private static void AppendJsonProperty(StringBuilder builder, string name, int value)
    {
        builder.Append('"').Append(EscapeJson(name)).Append("\":").Append(value);
    }

    private static void AppendJsonProperty(StringBuilder builder, string name, bool value)
    {
        builder.Append('"').Append(EscapeJson(name)).Append("\":").Append(value ? "true" : "false");
    }

    private static string EscapeJson(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }

        return builder.ToString();
    }
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
            SuggestedFix);
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

/// <summary>
/// 表示驗證報告的 JSON 匯出模型。
/// </summary>
public sealed class OdfValidationReportJsonModel
{
    /// <summary>
    /// 初始化 <see cref="OdfValidationReportJsonModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="isValid">文件是否通過驗證。</param>
    /// <param name="detectedVersion">偵測到的 ODF 版本。</param>
    /// <param name="documentKind">偵測到的文件種類。</param>
    /// <param name="infoCount">資訊性問題數量。</param>
    /// <param name="warningCount">警告問題數量。</param>
    /// <param name="errorCount">錯誤問題數量。</param>
    /// <param name="fatalCount">致命問題數量。</param>
    /// <param name="blockingIssueCount">會讓驗證失敗的問題數量。</param>
    /// <param name="issues">驗證問題匯出模型集合。</param>
    public OdfValidationReportJsonModel(
        bool isValid,
        string detectedVersion,
        string documentKind,
        int infoCount,
        int warningCount,
        int errorCount,
        int fatalCount,
        int blockingIssueCount,
        IReadOnlyList<OdfValidationIssueJsonModel> issues)
    {
        IsValid = isValid;
        DetectedVersion = detectedVersion ?? string.Empty;
        DocumentKind = documentKind ?? string.Empty;
        InfoCount = infoCount;
        WarningCount = warningCount;
        ErrorCount = errorCount;
        FatalCount = fatalCount;
        BlockingIssueCount = blockingIssueCount;
        Issues = issues ?? Array.Empty<OdfValidationIssueJsonModel>();
    }

    /// <summary>
    /// 取得文件是否通過驗證。
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 取得偵測到的 ODF 版本。
    /// </summary>
    public string DetectedVersion { get; }

    /// <summary>
    /// 取得偵測到的文件種類。
    /// </summary>
    public string DocumentKind { get; }

    /// <summary>
    /// 取得資訊性問題數量。
    /// </summary>
    public int InfoCount { get; }

    /// <summary>
    /// 取得警告問題數量。
    /// </summary>
    public int WarningCount { get; }

    /// <summary>
    /// 取得錯誤問題數量。
    /// </summary>
    public int ErrorCount { get; }

    /// <summary>
    /// 取得致命問題數量。
    /// </summary>
    public int FatalCount { get; }

    /// <summary>
    /// 取得會讓驗證失敗的問題數量。
    /// </summary>
    public int BlockingIssueCount { get; }

    /// <summary>
    /// 取得驗證問題匯出模型集合。
    /// </summary>
    public IReadOnlyList<OdfValidationIssueJsonModel> Issues { get; }
}

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
    public OdfValidationIssueJsonModel(
        string severity,
        string ruleId,
        string message,
        string? packagePath,
        string? xPath,
        string? requiredVersion,
        string? profileId,
        string suggestedFix)
    {
        Severity = severity ?? string.Empty;
        RuleId = ruleId ?? string.Empty;
        Message = message ?? string.Empty;
        PackagePath = packagePath;
        XPath = xPath;
        RequiredVersion = requiredVersion;
        ProfileId = profileId;
        SuggestedFix = suggestedFix ?? string.Empty;
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
}

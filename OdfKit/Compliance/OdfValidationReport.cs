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
    /// 取得一個值，表示驗證的文件是否符合選取的檢查專案。
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
    /// <returns>包含驗證報告摘要與問題清單的 JSON 匯出模型</returns>
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
    /// <returns>JSON 格式的驗證報告</returns>
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
        builder.Append(',');
        AppendJsonObject(builder, "details", issue.Details);
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

    private static void AppendJsonObject(StringBuilder builder, string name, IReadOnlyDictionary<string, string?> values)
    {
        builder.Append('"').Append(EscapeJson(name)).Append("\":{");
        int index = 0;
        foreach (KeyValuePair<string, string?> pair in values)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendJsonProperty(builder, pair.Key, pair.Value);
            index++;
        }

        builder.Append('}');
    }

    internal static IReadOnlyDictionary<string, string?> CopyDetails(IReadOnlyDictionary<string, string?>? details)
    {
        Dictionary<string, string?> copy = new(StringComparer.Ordinal);
        if (details is null)
        {
            return copy;
        }

        foreach (KeyValuePair<string, string?> pair in details)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
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

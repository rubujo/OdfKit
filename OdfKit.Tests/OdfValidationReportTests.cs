using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 驗證報告可供使用者與 CI 消費。
/// </summary>
public class OdfValidationReportTests
{
    /// <summary>
    /// 驗證報告提供穩定的 severity 計數與失敗狀態。
    /// </summary>
    [Fact]
    public void ValidationReportExposesStableSeverityCounts()
    {
        OdfValidationReport report = CreateReport();

        Assert.False(report.IsValid);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(1, report.FatalCount);
        Assert.Equal(2, report.BlockingIssueCount);
        Assert.Equal(3, report.IssuesBySeverity.Values.Sum());
    }

    /// <summary>
    /// 驗證每個 issue 都提供可讀的建議修復文字。
    /// </summary>
    [Fact]
    public void ValidationIssueExposesSuggestedFixText()
    {
        OdfValidationIssue issue = CreateReport().Issues[1];

        Assert.Equal("ODF1002", issue.RuleId);
        Assert.Contains("office:version", issue.SuggestedFix);
        Assert.Contains("content.xml", issue.SuggestedFix);
    }

    /// <summary>
    /// 驗證報告可匯出穩定 JSON 模型。
    /// </summary>
    [Fact]
    public void ValidationReportExportsStableJsonModel()
    {
        OdfValidationReportJsonModel model = CreateReport().ToJsonModel();

        Assert.False(model.IsValid);
        Assert.Equal("Odf14", model.DetectedVersion);
        Assert.Equal("Text", model.DocumentKind);
        Assert.Equal(1, model.WarningCount);
        Assert.Equal(1, model.ErrorCount);
        Assert.Equal(1, model.FatalCount);
        Assert.Equal(3, model.Issues.Count);
        Assert.Equal("ODF1002", model.Issues[1].RuleId);
        Assert.Equal("content.xml", model.Issues[1].PackagePath);
        Assert.Equal("/office:document-content[1]", model.Issues[1].XPath);
        Assert.Equal("OASIS_ODF_1_4_Strict", model.Issues[1].ProfileId);
        Assert.Contains("office:version", model.Issues[1].SuggestedFix);
        Assert.Equal("1.4", model.Issues[1].Details["expectedVersion"]);
        Assert.Equal("missing", model.Issues[1].Details["actualVersion"]);
    }

    /// <summary>
    /// 驗證 JSON 字串輸出包含 CI 可用欄位。
    /// </summary>
    [Fact]
    public void ValidationReportExportsJsonString()
    {
        string json = CreateReport().ToJson();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.False(root.GetProperty("isValid").GetBoolean());
        Assert.Equal("Odf14", root.GetProperty("detectedVersion").GetString());
        Assert.Equal(1, root.GetProperty("warningCount").GetInt32());
        Assert.Equal(1, root.GetProperty("errorCount").GetInt32());
        Assert.Equal(1, root.GetProperty("fatalCount").GetInt32());
        Assert.Equal("ODF1002", root.GetProperty("issues")[1].GetProperty("ruleId").GetString());
        Assert.Equal("加入或修正 content.xml 的 office:version。", root.GetProperty("issues")[1].GetProperty("suggestedFix").GetString());
        Assert.Equal("1.4", root.GetProperty("issues")[1].GetProperty("details").GetProperty("expectedVersion").GetString());
        Assert.Equal("missing", root.GetProperty("issues")[1].GetProperty("details").GetProperty("actualVersion").GetString());
    }

    private static OdfValidationReport CreateReport()
    {
        return new OdfValidationReport(
            OdfVersion.Odf14,
            OdfDocumentKind.Text,
            [
                new OdfValidationIssue(
                    OdfIssueSeverity.Warning,
                    "ODF0400",
                    "No office:version attribute was found.",
                    xPath: "/office:document-content[1]",
                    profileId: "OASIS_ODF_1_4_Strict"),
                new OdfValidationIssue(
                    OdfIssueSeverity.Error,
                    "ODF1002",
                    "Profile requires ODF 1.4.",
                    "content.xml",
                    "/office:document-content[1]",
                    OdfVersion.Odf14,
                    "OASIS_ODF_1_4_Strict",
                    new Dictionary<string, string?>
                    {
                        ["expectedVersion"] = "1.4",
                        ["actualVersion"] = "missing"
                    }),
                new OdfValidationIssue(
                    OdfIssueSeverity.Fatal,
                    "ODF0200",
                    "Zip Slip detected.",
                    "../evil.xml",
                    profileId: "ROC_Taiwan_ODF_CNS15251")
            ]);
    }
}

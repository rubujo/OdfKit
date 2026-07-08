using System.Linq;
using System.Text;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Image;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定實務互通性 validator 的常見風險檢查。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class PracticalCompatibilityValidatorTests
{
    /// <summary>
    /// 驗證巨集與腳本封裝項目會回報 portable editing 風險。
    /// </summary>
    [Fact]
    public void Validate_ReportsMacroOrScriptPackageEntries()
    {
        using TextDocument document = TextDocument.Create();
        document.Package.WriteEntry("Scripts/python/hello.py", Encoding.UTF8.GetBytes("print('hi')"), "text/x-python");

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0001" && issue.PackagePath == "Scripts/python/hello.py");
    }

    /// <summary>
    /// 驗證非標準圖片格式會回報跨工具編輯風險。
    /// </summary>
    [Fact]
    public void Validate_ReportsNonPortableImageMediaTypes()
    {
        using OdfImageDocument document = OdfImageDocument.Create();
        document.Package.WriteEntry("Pictures/source.bmp", [1, 2, 3, 4], "image/bmp");

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0002" && issue.PackagePath == "Pictures/source.bmp");
    }

    /// <summary>
    /// 驗證進階圖表對 Microsoft Office ODF profile 會回報呈現差異風險。
    /// </summary>
    [Fact]
    public void Validate_ReportsAdvancedChartForMicrosoftOfficeProfile()
    {
        using ChartDocument chart = ChartDocument.CreateBubble(
            "泡泡圖",
            new OdfBubbleChartSeriesRequest("Data.$A$2:.$A$4", "Data.$B$2:.$B$4", "Data.$C$2:.$C$4"));

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            chart,
            OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0200");
        Assert.DoesNotContain(
            OdfPracticalCompatibilityValidator.Validate(chart, OdfPracticalCompatibilityProfile.LibreOfficeCurrent).Issues,
            issue => issue.RuleId == "PRAC0200");
    }

    /// <summary>
    /// 驗證實務互通報告保留文件類型與本地化訊息。
    /// </summary>
    [Fact]
    public void Validate_ReportCarriesDocumentKindAndMessage()
    {
        using ChartDocument chart = ChartDocument.CreateStock(
            "股票圖",
            new OdfStockChartSeriesRequest("S.$A$2:.$A$4", "S.$B$2:.$B$4", "S.$C$2:.$C$4", "S.$D$2:.$D$4"));

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            chart,
            OdfPracticalCompatibilityProfile.PortableEditing);

        OdfPracticalCompatibilityIssue issue = Assert.Single(report.Issues.Where(i => i.RuleId == "PRAC0200"));
        Assert.Equal(chart.DocumentKind, issue.DocumentKind);
        Assert.False(string.IsNullOrWhiteSpace(issue.Message));
        Assert.False(string.IsNullOrWhiteSpace(issue.Suggestion));
    }
}

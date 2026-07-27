using System.Linq;
using System.Text;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
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
    public void ValidateReportsMacroOrScriptPackageEntries()
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
    public void ValidateReportsNonPortableImageMediaTypes()
    {
        using OdfImageDocument document = OdfImageDocument.Create();
        document.Package.WriteEntry("Pictures/source.bmp", [1, 2, 3, 4], "image/bmp");

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0002" && issue.PackagePath == "Pictures/source.bmp");
    }

    /// <summary>
    /// 驗證 ImageDocument inspection issue 會整合到實務相容性報告。
    /// </summary>
    [Fact]
    public void ValidateIncludesImageInspectionIssuesForImageDocuments()
    {
        using OdfImageDocument document = OdfImageDocument.Create();
        document.AddImageFrame([1, 2, 3, 4], 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm(), "risk.bmp", "RiskFrame");
        document.SetImageRotation("RiskFrame", 30);

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing);

        Assert.Contains(report.Issues, issue => issue.RuleId == "IMG0001" && issue.MessageKey == "Msg_ImageInspection_NonPortableMediaType");
        Assert.Contains(report.Issues, issue => issue.RuleId == "IMG0004");
    }

    /// <summary>
    /// 驗證進階圖表對 Microsoft Office ODF profile 會回報呈現差異風險。
    /// </summary>
    [Fact]
    public void ValidateReportsAdvancedChartForMicrosoftOfficeProfile()
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
    /// 驗證複雜 ODT 結構會對 Microsoft Office profile 回報 Word 復原提示風險。
    /// </summary>
    [Fact]
    public void ValidateReportsWordOdtRepairRiskForComplexTextStructures()
    {
        using TextDocument document = TextDocument.Create();
        document.AddTableOfContents("目錄", 1);
        _ = document.AddSection("ExecutiveSection", 2, 0.5.Cm());

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf);

        OdfPracticalCompatibilityIssue issue = Assert.Single(report.Issues, i => i.RuleId == "PRAC0301");
        Assert.Equal("content.xml", issue.PackagePath);
        Assert.Equal("True", issue.Details?["hasTextIndex"]);
        Assert.Equal("True", issue.Details?["hasTextSection"]);
        Assert.DoesNotContain(
            OdfPracticalCompatibilityValidator.Validate(document, OdfPracticalCompatibilityProfile.LibreOfficeCurrent).Issues,
            i => i.RuleId == "PRAC0301");
    }

    /// <summary>
    /// 驗證 ODS 嵌入進階圖表也會依 profile 回報呈現差異風險。
    /// </summary>
    [Fact]
    public void ValidateReportsEmbeddedAdvancedChartForMicrosoftOfficeProfile()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetValues(
            new OdfCellAddress(0, 0, "Data"),
            new object?[,]
            {
                { "Name", "Value" },
                { "A", 10d },
                { "B", 20d },
            });

        _ = document.InsertChartFromRange(
            "Data",
            new OdfCellAddress(0, 3, "Data"),
            new OdfCellRange(0, 0, 2, 1, "Data"),
            new OdfEmbeddedChartOptions { Preset = OdfChartPreset.Column3D });

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0200" && issue.PackagePath == "Object 1/content.xml");
    }

    /// <summary>
    /// 驗證 ODS 明確列高欄寬會依 profile 回報跨套件風險。
    /// </summary>
    [Fact]
    public void ValidateReportsSpreadsheetSizingForPortableProfiles()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetColumnWidth(0, 2.Cm());
        sheet.SetRowHeight(0, 1.Cm());

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0400");
        Assert.DoesNotContain(
            OdfPracticalCompatibilityValidator.Validate(document, OdfPracticalCompatibilityProfile.LibreOfficeCurrent).Issues,
            issue => issue.RuleId == "PRAC0400");
    }

    /// <summary>
    /// 驗證 ODG 裁切或旋轉圖片會對 portable editing 回報風險。
    /// </summary>
    [Fact]
    public void ValidateReportsImageTransformForDrawingDocuments()
    {
        using DrawingDocument document = DrawingDocument.Create();
        OdfPicture picture = document.Pages.Add("Canvas")
            .AddPicture([1, 2, 3, 4], 1.Cm(), 1.Cm(), 2.Cm(), 2.Cm());
        picture.Node.SetAttribute("transform", OdfNamespaces.Draw, "rotate(0.5)", "draw");

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.MicrosoftOfficeOdf);

        Assert.Contains(report.Issues, issue => issue.RuleId == "PRAC0500");
    }

    /// <summary>
    /// 驗證實務互通報告保留文件類型與本地化訊息。
    /// </summary>
    [Fact]
    public void ValidateReportCarriesDocumentKindAndMessage()
    {
        using ChartDocument chart = ChartDocument.CreateStock(
            "股票圖",
            new OdfStockChartSeriesRequest("S.$A$2:.$A$4", "S.$B$2:.$B$4", "S.$C$2:.$C$4", "S.$D$2:.$D$4"));

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            chart,
            OdfPracticalCompatibilityProfile.PortableEditing);

        OdfPracticalCompatibilityIssue issue = Assert.Single(report.Issues, i => i.RuleId == "PRAC0200");
        Assert.Equal(chart.DocumentKind, issue.DocumentKind);
        Assert.False(string.IsNullOrWhiteSpace(issue.Message));
        Assert.False(string.IsNullOrWhiteSpace(issue.Suggestion));
    }

    /// <summary>
    /// 驗證 validator options 可停用規則、覆寫嚴重性與限制回傳數量。
    /// </summary>
    [Fact]
    public void ValidateOptionsFilterOverrideAndLimitIssues()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetColumnWidth(0, 2.Cm());
        sheet.SetPrintArea(new OdfCellRange(0, 0, 5, 2, "Data"));

        var options = new OdfPracticalCompatibilityOptions { MaximumIssueCount = 1 };
        options.DisabledRuleIds.Add("PRAC0400");
        options.SeverityOverrides["PRAC0401"] = OdfIssueSeverity.Info;

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing,
            options);

        OdfPracticalCompatibilityIssue issue = Assert.Single(report.Issues);
        Assert.Equal("PRAC0401", issue.RuleId);
        Assert.Equal(OdfIssueSeverity.Info, issue.Severity);
    }

    /// <summary>
    /// 驗證 maximum issue count 為 0 時會回傳空清單。
    /// </summary>
    [Fact]
    public void ValidateOptionsCanSuppressAllIssuesWithZeroLimit()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Data");
        sheet.SetColumnWidth(0, 2.Cm());
        sheet.SetPrintArea(new OdfCellRange(0, 0, 5, 2, "Data"));

        OdfPracticalCompatibilityReport report = OdfPracticalCompatibilityValidator.Validate(
            document,
            OdfPracticalCompatibilityProfile.PortableEditing,
            new OdfPracticalCompatibilityOptions { MaximumIssueCount = 0 });

        Assert.Empty(report.Issues);
    }
}

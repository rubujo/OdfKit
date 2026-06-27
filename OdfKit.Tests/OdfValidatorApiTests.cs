using System;
using System.IO;
using System.Threading.Tasks;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證公開 ODF 驗證入口 API。
/// </summary>
public class OdfValidatorApiTests
{
    /// <summary>
    /// 驗證可直接以檔案路徑驗證封裝 ODF 文件。
    /// </summary>
    [Fact]
    public void ValidatorCanValidatePackagePath()
    {
        string path = Path.Combine(Path.GetTempPath(), "odfkit-validator-" + Guid.NewGuid().ToString("N") + ".odt");
        try
        {
            using (FileStream stream = File.Create(path))
            using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
            {
                package.Save();
            }

            OdfValidationReport report = OdfValidator.Validate(path);

            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
            Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
            Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 驗證可直接以串流與檔名驗證扁平 XML ODF 文件。
    /// </summary>
    [Fact]
    public void ValidatorCanValidateFlatXmlStream()
    {
        using var stream = new MemoryStream();
        OdfDocumentFactory.WriteFlatXml(stream, OdfDocumentKind.FlatSpreadsheet, leaveOpen: true);
        stream.Position = 0;

        OdfValidationReport report = OdfValidator.Validate(
            stream,
            "workbook.fods",
            OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.FlatSpreadsheet, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }

    /// <summary>
    /// 驗證補充的扁平 XML ODF 副檔名會對應到正確文件種類。
    /// </summary>
    [Theory]
    [InlineData(OdfDocumentKind.FlatChart, "chart.fodc")]
    [InlineData(OdfDocumentKind.FlatFormula, "formula.fdf")]
    [InlineData(OdfDocumentKind.FlatImage, "image.fodi")]
    public void FlatOdf_SupplementalExtensions_ValidateWithExpectedKind(OdfDocumentKind expectedKind, string fileName)
    {
        using var stream = new MemoryStream();
        OdfDocumentFactory.WriteFlatXml(stream, expectedKind, leaveOpen: true);
        stream.Position = 0;

        OdfValidationReport report = OdfValidator.Validate(
            stream,
            fileName,
            OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(expectedKind, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }

    /// <summary>
    /// 驗證可用選項物件驗證已開啟的 ODF 封裝。
    /// </summary>
    [Fact]
    public void ValidatorCanValidateOpenPackageWithOptions()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Presentation, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        using OdfPackage opened = OdfPackage.Open(stream, leaveOpen: true);

        OdfValidationReport report = OdfValidator.Validate(
            opened,
            new OdfValidationOptions
            {
                FileName = "slides.odp",
                Profile = OdfComplianceProfiles.OasisOdf14Extended
            });

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Presentation, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }

    /// <summary>
    /// 驗證嚴格選項捷徑可用於公開驗證入口。
    /// </summary>
    [Fact]
    public void ValidatorStrictOptionsExposeOdf14StrictProfile()
    {
        using var stream = new MemoryStream();
        OdfDocumentFactory.WriteFlatXml(stream, OdfDocumentKind.FlatText, leaveOpen: true);
        stream.Position = 0;

        OdfValidationReport report = OdfValidator.Validate(
            stream,
            new OdfValidationOptions
            {
                FileName = "document.fodt",
                Profile = OdfComplianceProfiles.OasisOdf14Strict
            });

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfComplianceProfiles.OasisOdf14Strict.Id, OdfValidationOptions.Odf14Strict.Profile?.Id);
    }

    /// <summary>
    /// 驗證文件實例可直接呼叫 <see cref="OdfDocument.Validate(OdfComplianceProfile?)"/>，
    /// 且能反映呼叫前所做但尚未儲存的編輯。
    /// </summary>
    [Fact]
    public void DocumentInstance_Validate_ReflectsUnsavedEdits()
    {
        using TextDocument doc = TextDocument.Create();
        doc.Title = "Validate() 骨架測試";
        doc.AddParagraph("尚未儲存的段落");

        OdfValidationReport report = doc.Validate();

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
    }

    /// <summary>
    /// 驗證文件實例可直接呼叫 <see cref="OdfDocument.ValidateAsync(OdfComplianceProfile?, System.Threading.CancellationToken)"/>。
    /// </summary>
    [Fact]
    public async Task DocumentInstance_ValidateAsync_ReturnsStructuredReport()
    {
        using TextDocument doc = TextDocument.Create();
        doc.AddParagraph("非同步驗證測試");

        OdfValidationReport report = await doc.ValidateAsync(OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
    }

    /// <summary>
    /// 驗證文件級 <see cref="OdfDocument.Validate(OdfComplianceProfile?)"/> 並非僅限於 Text，
    /// 而是因定義於 <see cref="OdfDocument"/> 基底類別而對所有文件種類（包含次要格式）皆可用，
    /// 完成 Workstream E「統一驗證 API」對全格式的覆蓋。
    /// </summary>
    [Fact]
    public void DocumentInstance_Validate_AcrossSecondaryFormatKinds_AllSucceed()
    {
        using ChartDocument chart = ChartDocument.Create(new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "驗證測試圖表",
            DataRange = new OdfCellRange(0, 0, 4, 1, "LocalTable"),
            HasLegend = false
        });
        OdfValidationReport chartReport = chart.Validate();
        Assert.True(chartReport.IsValid, string.Join(Environment.NewLine, chartReport.Issues));

        using OdfFormulaDocument formula = OdfFormulaDocument.Create();
        formula.SetIdentifierEquation("x", "y");
        OdfValidationReport formulaReport = formula.Validate();
        Assert.True(formulaReport.IsValid, string.Join(Environment.NewLine, formulaReport.Issues));

        using OdfImageDocument image = OdfImageDocument.Create();
        OdfValidationReport imageReport = image.Validate();
        Assert.True(imageReport.IsValid, string.Join(Environment.NewLine, imageReport.Issues));

        using OdfDatabaseDocument database = OdfDatabaseDocument.Create();
        OdfValidationReport databaseReport = database.Validate();
        Assert.True(databaseReport.IsValid, string.Join(Environment.NewLine, databaseReport.Issues));
    }

    /// <summary>
    /// 驗證文件級 <see cref="OdfDocument.Validate(OdfComplianceProfile?)"/> 的負向案例：
    /// 插入未註冊命名空間／元素名稱的節點後，以嚴格相容性設定檔驗證應回報失敗，而非靜默通過。
    /// </summary>
    [Fact]
    public void DocumentInstance_Validate_DetectsUnregisteredElementUnderStrictProfile()
    {
        using TextDocument doc = TextDocument.Create();
        doc.AddParagraph("正常段落");

        // 使用已知命名空間（text:）但 schema 未定義的本地名稱，觸發嚴格 RNG 結構驗證失敗；
        // 完全陌生的命名空間反而會被視為外部擴充標記而略過，不構成有效的負向測試案例。
        OdfNode bogus = OdfNodeFactory.CreateElement("totally-bogus-element", OdfNamespaces.Text, "text");
        doc.BodyTextRoot.AppendChild(bogus);

        OdfValidationReport report = doc.Validate(OdfComplianceProfiles.OasisOdf14Strict);

        Assert.False(report.IsValid, "插入未註冊命名空間的元素應使嚴格相容性設定檔驗證失敗。");
        Assert.NotEmpty(report.Issues);
    }

    /// <summary>
    /// 驗證文件級 <see cref="OdfDocument.Validate(OdfComplianceProfile?)"/> 會合併記憶體 DOM 拓撲檢查結果。
    /// </summary>
    [Fact]
    public void DocumentInstance_Validate_DetectsInMemoryDomTopologyErrors()
    {
        using TextDocument doc = TextDocument.Create();
        doc.AddParagraph("正常段落");
        OdfNode orphanCell = OdfNodeFactory.CreateElement("table-cell", OdfNamespaces.Table, "table");
        doc.ContentDom.AppendChild(orphanCell);

        OdfValidationReport report = doc.Validate();

        Assert.False(report.IsValid);
        OdfValidationIssue issue = Assert.Single(
            report.Issues,
            issue => issue.RuleId == "Rule_Topology_OrphanCell");
        Assert.Equal(OdfIssueSeverity.Error, issue.Severity);
        Assert.Equal("content.xml", issue.PackagePath);
        Assert.Contains("table-cell", issue.Message, StringComparison.Ordinal);
        Assert.Contains("table-cell", issue.XPath, StringComparison.Ordinal);
    }
}

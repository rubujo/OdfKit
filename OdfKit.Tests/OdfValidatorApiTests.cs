using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
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
    /// 驗證補充的扁平 XML ODF 副檔名會映射到正確文件種類。
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
}

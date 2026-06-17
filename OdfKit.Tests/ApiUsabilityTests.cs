using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定高階 API 的使用者故事形狀。
/// </summary>
public class ApiUsabilityTests
{
    /// <summary>
    /// 驗證使用少量程式碼即可建立含兩個段落的 ODT。
    /// </summary>
    [Fact]
    public void CreateOdtWithTwoParagraphsInFewLines()
    {
        using var document = (TextDocument)OdfDocument.Create(OdfDocumentKind.Text);
        document.AddParagraph("第一段");
        document.AddParagraph("第二段");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("第一段", xml);
        Assert.Contains("第二段", xml);
        Assert.Equal("application/vnd.oasis.opendocument.text", package.MimeType);
    }

    /// <summary>
    /// 驗證使用少量程式碼即可建立含儲存格與公式的 ODS。
    /// </summary>
    [Fact]
    public void CreateOdsWithCellAndFormulaInFewLines()
    {
        using var workbook = (SpreadsheetDocument)OdfDocument.Create(OdfDocumentKind.Spreadsheet);
        OdfTableSheet sheet = workbook.AddSheet("Sheet1");
        sheet.GetCell("A1").SetValue(40d);
        sheet.GetCell("B1").SetValue(2d);
        sheet.GetCell("C1").Formula = "of:=[.A1]+[.B1]";

        using var stream = new MemoryStream();
        workbook.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream content = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(content);
        string xml = reader.ReadToEnd();

        Assert.Contains("Sheet1", xml);
        Assert.Contains("of:=[.A1]+[.B1]", xml);
        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", package.MimeType);
    }

    /// <summary>
    /// 驗證可用任意 ODF 高階入口載入並保存為相同格式。
    /// </summary>
    [Fact]
    public void LoadAnyOdfAndSaveAsSameFormat()
    {
        using var source = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(source, OdfDocumentKind.Presentation, leaveOpen: true))
        {
            package.Save();
        }

        source.Position = 0;
        using OdfDocument document = OdfDocument.Load(source, "slides.odp");

        using var saved = new MemoryStream();
        document.SaveToStream(saved);
        saved.Position = 0;

        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
        Assert.Equal("application/vnd.oasis.opendocument.presentation", reopened.MimeType);
        Assert.True(reopened.HasEntry("content.xml"));
    }

    /// <summary>
    /// 驗證可用少量程式碼驗證任意 ODF 文件。
    /// </summary>
    [Fact]
    public void ValidateAnyOdfInFewLines()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;
        OdfValidationReport report = OdfValidator.Validate(
            stream,
            "document.odt",
            OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
        Assert.Equal(OdfVersion.Odf14, report.DetectedVersion);
    }
}

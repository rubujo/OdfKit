using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using OdfKit.Drawing;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 範本建立、修改、套用與 Save 流程的 Round-trip 測試。
/// </summary>
public class TemplateRoundTripTests
{
    /// <summary>
    /// 驗證從 OTT 文字範本建立 ODT 文件的完整流程。
    /// </summary>
    [Fact]
    public void TextTemplateInstantiationAndRoundTrip()
    {
        using var template = TextTemplateDocument.Create();
        template.Title = "My Template Title";
        template.Creator = "Template Creator";

        var masterPage = template.AddMasterPage("MyTemplateMaster");
        masterPage.HeaderText = "Template Header";
        masterPage.FooterText = "Template Footer";

        using var doc = TextDocument.CreateFromTemplate(template);

        Assert.Equal("application/vnd.oasis.opendocument.text", doc.Package.MimeType);
        Assert.Equal(OdfVersion.Odf14, doc.Package.Version);

        var copiedMasterPage = doc.GetMasterPages().FirstOrDefault(m => m.Name == "MyTemplateMaster");
        Assert.NotNull(copiedMasterPage);
        Assert.Equal("Template Header", copiedMasterPage.HeaderText);
        Assert.Equal("Template Footer", copiedMasterPage.FooterText);

        Assert.Null(doc.Creator);
        Assert.Null(doc.TemplateMetadata);

        doc.TemplateMetadata = new OdfTemplateMetadata
        {
            Href = "http://templates.example.com/mytemplate.ott",
            Title = "My Original OTT Template",
            Date = DateTime.UtcNow
        };

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var loadedDoc = TextDocument.Load(ms);
        Assert.NotNull(loadedDoc.TemplateMetadata);
        Assert.Equal("http://templates.example.com/mytemplate.ott", loadedDoc.TemplateMetadata.Href);
        Assert.Equal("My Original OTT Template", loadedDoc.TemplateMetadata.Title);

        var loadedMaster = loadedDoc.GetMasterPages().FirstOrDefault(m => m.Name == "MyTemplateMaster");
        Assert.NotNull(loadedMaster);
        Assert.Equal("Template Header", loadedMaster.HeaderText);
    }

    /// <summary>
    /// 驗證使用者欄位（範本變數）宣告可新增、更新並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void UserFieldDeclarations_RoundTripAfterSaveAndLoad()
    {
        using var template = TextTemplateDocument.Create();
        template.AddUserFieldDeclaration("CustomerName", "string", "預設客戶");
        template.AddUserFieldDeclaration("OrderTotal", "float", "100");

        Assert.True(template.SetUserFieldValue("CustomerName", "王小明"));
        Assert.False(template.SetUserFieldValue("NotExist", "x"));

        using var stream = new MemoryStream();
        template.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextTemplateDocument.Load(stream);
        var decls = loaded.GetUserFieldDeclarations();
        Assert.Equal(2, decls.Count);

        var customerName = decls.Single(d => d.Name == "CustomerName");
        Assert.Equal("string", customerName.ValueType);
        Assert.Equal("王小明", customerName.Value);

        var orderTotal = decls.Single(d => d.Name == "OrderTotal");
        Assert.Equal("float", orderTotal.ValueType);
        Assert.Equal("100", orderTotal.Value);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDocument.ConvertZipToFlatXml"/> 與 <see cref="OdfDocument.ConvertFlatXmlToZip"/> 的往返一致性。
    /// </summary>
    [Fact]
    public void ConvertZipToFlatXmlAndBack_RoundTrips()
    {
        using var doc = TextDocument.Create();
        doc.Title = "Flat XML 轉換測試";

        string zipPath = Path.Combine(Path.GetTempPath(), $"odfkit-test-{Guid.NewGuid():N}.odt");
        string flatPath = Path.Combine(Path.GetTempPath(), $"odfkit-test-{Guid.NewGuid():N}.fodt");
        string roundTripZipPath = Path.Combine(Path.GetTempPath(), $"odfkit-test-{Guid.NewGuid():N}.odt");

        try
        {
            doc.Save(zipPath);

            OdfDocument.ConvertZipToFlatXml(zipPath, flatPath);
            Assert.True(File.Exists(flatPath));
            string flatContent = File.ReadAllText(flatPath);
            Assert.Contains("<office:document", flatContent);

            OdfDocument.ConvertFlatXmlToZip(flatPath, roundTripZipPath);
            using var reloaded = TextDocument.Load(roundTripZipPath);
            Assert.Equal("Flat XML 轉換測試", reloaded.Title);
        }
        finally
        {
            File.Delete(zipPath);
            File.Delete(flatPath);
            File.Delete(roundTripZipPath);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.CreateFromTemplate(TextTemplateDocument, bool)"/> 的
    /// <c>clearUserContent</c> 選項可清除範本段落內容但保留母片頁面樣式。
    /// </summary>
    [Fact]
    public void TextTemplate_ClearUserContent_RemovesParagraphsKeepsMasterPage()
    {
        using var template = TextTemplateDocument.Create();
        var masterPage = template.AddMasterPage("MyTemplateMaster");
        masterPage.HeaderText = "Template Header";
        template.AddParagraph("範本既有內容");

        using var doc = TextDocument.CreateFromTemplate(template, clearUserContent: true);

        Assert.Empty(doc.BodyTextRoot.Children);
        var copiedMasterPage = doc.GetMasterPages().FirstOrDefault(m => m.Name == "MyTemplateMaster");
        Assert.NotNull(copiedMasterPage);
        Assert.Equal("Template Header", copiedMasterPage!.HeaderText);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.CreateFromTemplate(SpreadsheetTemplateDocument, bool)"/> 的
    /// <c>clearUserContent</c> 選項可清除工作表資料列但保留欄寬設定。
    /// </summary>
    [Fact]
    public void SpreadsheetTemplate_ClearUserContent_RemovesRowsKeepsColumnWidth()
    {
        using var template = SpreadsheetTemplateDocument.Create();
        var sheet = template.Worksheets.Add("Sheet1");
        sheet.Cells["A1"].CellValue = "範本既有資料";
        sheet.SetColumnWidth(0, OdfLength.FromCentimeters(5));

        using var doc = SpreadsheetDocument.CreateFromTemplate(template, clearUserContent: true);

        var copiedSheet = doc.Worksheets["Sheet1"];
        Assert.True(string.IsNullOrEmpty(copiedSheet.Cells["A1"].CellValue?.ToString()));
    }

    /// <summary>
    /// 驗證從 OTS 試算表範本建立 ODS 文件的完整流程，涵蓋母片頁面、範本中繼資料與
    /// 工作表內容的儲存／載入 Round-trip。
    /// </summary>
    [Fact]
    public void SpreadsheetTemplateInstantiationAndRoundTrip()
    {
        using var template = SpreadsheetTemplateDocument.Create();
        template.Title = "My Spreadsheet Template";

        var masterPage = template.AddMasterPage("MySpreadsheetMaster");
        masterPage.HeaderText = "Spreadsheet Header";

        var sheet = template.Worksheets.Add("Sheet1");
        sheet.Cells["A1"].CellValue = "範本既有資料";

        using var doc = SpreadsheetDocument.CreateFromTemplate(template);

        Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", doc.Package.MimeType);

        var copiedMasterPage = doc.GetMasterPages().FirstOrDefault(m => m.Name == "MySpreadsheetMaster");
        Assert.NotNull(copiedMasterPage);
        Assert.Equal("Spreadsheet Header", copiedMasterPage!.HeaderText);

        doc.TemplateMetadata = new OdfTemplateMetadata
        {
            Href = "http://templates.example.com/mytemplate.ots",
            Title = "My Original OTS Template",
            Date = DateTime.UtcNow
        };

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var loadedDoc = SpreadsheetDocument.Load(ms);
        Assert.NotNull(loadedDoc.TemplateMetadata);
        Assert.Equal("http://templates.example.com/mytemplate.ots", loadedDoc.TemplateMetadata.Href);

        var loadedMaster = loadedDoc.GetMasterPages().FirstOrDefault(m => m.Name == "MySpreadsheetMaster");
        Assert.NotNull(loadedMaster);
        Assert.Equal("Spreadsheet Header", loadedMaster!.HeaderText);

        var loadedSheet = loadedDoc.Worksheets["Sheet1"];
        Assert.Equal("範本既有資料", loadedSheet.Cells["A1"].CellValue?.ToString());
    }

    /// <summary>
    /// 驗證從 OTP 簡報範本建立 ODP 文件的完整流程，涵蓋母片頁面與範本中繼資料的
    /// 儲存／載入 Round-trip。
    /// </summary>
    [Fact]
    public void PresentationTemplateInstantiationAndRoundTrip()
    {
        using var template = PresentationTemplateDocument.Create();
        template.Title = "My Presentation Template";

        var masterPage = template.AddMasterPage("MyPresentationMaster");

        using var doc = PresentationDocument.CreateFromTemplate(template);

        Assert.Equal("application/vnd.oasis.opendocument.presentation", doc.Package.MimeType);

        var copiedMasterPage = doc.GetMasterPages().FirstOrDefault(m => m.Name == "MyPresentationMaster");
        Assert.NotNull(copiedMasterPage);

        doc.TemplateMetadata = new OdfTemplateMetadata
        {
            Href = "http://templates.example.com/mytemplate.otp",
            Title = "My Original OTP Template",
            Date = DateTime.UtcNow
        };

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var loadedDoc = PresentationDocument.Load(ms);
        Assert.NotNull(loadedDoc.TemplateMetadata);
        Assert.Equal("http://templates.example.com/mytemplate.otp", loadedDoc.TemplateMetadata.Href);

        var loadedMaster = loadedDoc.GetMasterPages().FirstOrDefault(m => m.Name == "MyPresentationMaster");
        Assert.NotNull(loadedMaster);
    }

    /// <summary>
    /// 驗證從 OTG 繪圖範本建立 ODG 文件的完整流程，涵蓋母片頁面與範本中繼資料的
    /// 儲存／載入 Round-trip。
    /// </summary>
    [Fact]
    public void DrawingTemplateInstantiationAndRoundTrip()
    {
        using var template = GraphicsTemplateDocument.Create();
        template.Title = "My Drawing Template";

        var masterPage = template.AddMasterPage("MyDrawingMaster");

        using var doc = DrawingDocument.CreateFromTemplate(template);

        Assert.Equal("application/vnd.oasis.opendocument.graphics", doc.Package.MimeType);

        var copiedMasterPage = doc.GetMasterPages().FirstOrDefault(m => m.Name == "MyDrawingMaster");
        Assert.NotNull(copiedMasterPage);

        doc.TemplateMetadata = new OdfTemplateMetadata
        {
            Href = "http://templates.example.com/mytemplate.otg",
            Title = "My Original OTG Template",
            Date = DateTime.UtcNow
        };

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using var loadedDoc = DrawingDocument.Load(ms);
        Assert.NotNull(loadedDoc.TemplateMetadata);
        Assert.Equal("http://templates.example.com/mytemplate.otg", loadedDoc.TemplateMetadata.Href);

        var loadedMaster = loadedDoc.GetMasterPages().FirstOrDefault(m => m.Name == "MyDrawingMaster");
        Assert.NotNull(loadedMaster);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSection.IsProtected"/> 可寫入並於儲存／載入後讀回，
    /// 用於將範本中特定區段標記為唯讀。
    /// </summary>
    [Fact]
    public void Section_IsProtected_RoundTripsAfterSaveAndLoad()
    {
        using var doc = TextDocument.Create();
        OdfSection section = doc.AddSection("ReadOnlySection", 1, OdfLength.FromCentimeters(0));
        section.IsProtected = true;

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextDocument.Load(stream);
        OdfNode? sectionNode = loaded.BodyTextRoot.Children
            .FirstOrDefault(n => n.LocalName == "section" && n.GetAttribute("name", OdfNamespaces.Text) == "ReadOnlySection");
        Assert.NotNull(sectionNode);
        var reloadedSection = new OdfSection(sectionNode!, loaded);
        Assert.True(reloadedSection.IsProtected);
    }
}

using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
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
}

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
}

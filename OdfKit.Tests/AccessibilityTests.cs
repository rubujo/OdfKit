using System.IO;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Z-1 無障礙屬性 API 的整合測試。
/// </summary>
public class AccessibilityTests
{
    private static string GetContentXml(TextDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("content.xml");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    private static string GetMetaXml(TextDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("meta.xml");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    private static string GetContentXml(SpreadsheetDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("content.xml");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// 驗證 OdfImage.AltText 寫入 svg:desc 節點至 draw:frame 中。
    /// </summary>
    [Fact]
    public void OdfImage_AltText_WritesSvgDescNode()
    {
        using var doc = TextDocument.Create();
        var img = doc.Body.Images.Add(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(3));

        img.AltText = "圖示說明文字";

        string xml = GetContentXml(doc);

        Assert.Contains("svg:desc", xml);
        Assert.Contains("圖示說明文字", xml);
    }

    /// <summary>
    /// 驗證 OdfImage.AccessibilityTitle 寫入 svg:title 節點。
    /// </summary>
    [Fact]
    public void OdfImage_AccessibilityTitle_WritesSvgTitleNode()
    {
        using var doc = TextDocument.Create();
        var img = doc.Body.Images.Add(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            OdfLength.FromCentimeters(5),
            OdfLength.FromCentimeters(3));

        img.AccessibilityTitle = "Logo 圖示";

        string xml = GetContentXml(doc);

        Assert.Contains("svg:title", xml);
        Assert.Contains("Logo 圖示", xml);
    }

    /// <summary>
    /// 驗證 AltText 和 AccessibilityTitle 可同時存在。
    /// </summary>
    [Fact]
    public void OdfImage_AltTextAndTitle_BothWritten()
    {
        using var doc = TextDocument.Create();
        var img = doc.Body.Images.Add(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3));

        img.AltText = "替代文字";
        img.AccessibilityTitle = "標題文字";

        string xml = GetContentXml(doc);

        Assert.Contains("svg:desc", xml);
        Assert.Contains("svg:title", xml);
        Assert.Contains("替代文字", xml);
        Assert.Contains("標題文字", xml);
    }

    /// <summary>
    /// 驗證 AltText getter 能讀回設定的值。
    /// </summary>
    [Fact]
    public void OdfImage_AltText_GetterReturnsSetValue()
    {
        using var doc = TextDocument.Create();
        var img = doc.Body.Images.Add(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3));

        img.AltText = "測試文字";
        Assert.Equal("測試文字", img.AltText);
    }

    /// <summary>
    /// 驗證設定 AltText 為 null 時移除 svg:desc 節點。
    /// </summary>
    [Fact]
    public void OdfImage_AltText_SetNull_RemovesSvgDesc()
    {
        using var doc = TextDocument.Create();
        var img = doc.Body.Images.Add(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            OdfLength.FromCentimeters(4),
            OdfLength.FromCentimeters(3));

        img.AltText = "測試";
        img.AltText = null;

        string xml = GetContentXml(doc);
        Assert.DoesNotContain("svg:desc", xml);
    }

    /// <summary>
    /// 驗證 OdfDocumentMetadata.Language 寫入 dc:language 至 meta.xml。
    /// </summary>
    [Fact]
    public void DocumentMetadata_Language_WritesDcLanguage()
    {
        using var doc = TextDocument.Create();
        doc.Metadata.Language = "zh-TW";

        string metaXml = GetMetaXml(doc);

        Assert.Contains("dc:language", metaXml);
        Assert.Contains("zh-TW", metaXml);
    }

    /// <summary>
    /// 驗證 OdfDocumentMetadata.Language getter 讀回設定的語言代碼。
    /// </summary>
    [Fact]
    public void DocumentMetadata_Language_GetterReturnsSetValue()
    {
        using var doc = TextDocument.Create();
        doc.Metadata.Language = "en-US";
        Assert.Equal("en-US", doc.Metadata.Language);
    }

    /// <summary>
    /// 驗證 OdfTable.Summary 寫入 table:summary 屬性。
    /// </summary>
    [Fact]
    public void OdfTable_Summary_WritesTableSummaryAttribute()
    {
        using var doc = TextDocument.Create();
        var table = doc.AddTable(3, 3);
        table.Summary = "季度銷售資料表，包含三個月的銷售數字";

        string xml = GetContentXml(doc);

        Assert.Contains("table:summary=\"季度銷售資料表，包含三個月的銷售數字\"", xml);
    }

    /// <summary>
    /// 驗證 OdfTable.Summary getter 讀回設定的值。
    /// </summary>
    [Fact]
    public void OdfTable_Summary_GetterReturnsSetValue()
    {
        using var doc = TextDocument.Create();
        var table = doc.AddTable(2, 2);
        table.Summary = "測試摘要";
        Assert.Equal("測試摘要", table.Summary);
    }

    /// <summary>
    /// 驗證 OdfTableSheet.Summary 寫入試算表工作表的 table:summary 屬性。
    /// </summary>
    [Fact]
    public void SpreadsheetSheet_Summary_WritesTableSummaryAttribute()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Sheet1");
        sheet.Summary = "每月營收試算表";

        string xml = GetContentXml(doc);

        Assert.Contains("table:summary=\"每月營收試算表\"", xml);
    }

    /// <summary>
    /// 驗證 OdfTableSheet.Summary getter 讀回設定的值。
    /// </summary>
    [Fact]
    public void SpreadsheetSheet_Summary_GetterReturnsSetValue()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("Data");
        sheet.Summary = "資料摘要";
        Assert.Equal("資料摘要", sheet.Summary);
    }
}

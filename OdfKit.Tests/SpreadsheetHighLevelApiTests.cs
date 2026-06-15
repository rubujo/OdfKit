using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定試算表高階 API 的整合測試。
/// </summary>
public class SpreadsheetHighLevelApiTests
{
    /// <summary>
    /// 驗證嵌入圖表建立 (AddChart) API 的正確性與封裝子文件結構。
    /// </summary>
    [Fact]
    public void AddChartCreatesCorrectSubDocuments()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");

        var chart = new OdfChartDefinition
        {
            ChartType = OdfChartType.Line,
            Title = "銷售折線圖",
            DataRange = new OdfCellRange(0, 0, 9, 1, "Sheet1"), // A1:B10
            HasLegend = true
        };

        // 錨定在 D1
        document.AddChart("Sheet1", new OdfCellAddress(0, 3, "Sheet1"), chart);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        
        // 1. 驗證 content.xml 內的主框架參照
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        Assert.Contains("table:shapes", contentXml);
        Assert.Contains("draw:frame", contentXml);
        Assert.Contains("draw:object xlink:href=\"./Object 1\"", contentXml);
        Assert.Contains("table:start-cell-address=\"Sheet1.D1\"", contentXml);

        // 2. 驗證 Object 1/mimetype 媒體類型
        byte[] mimeBytes = package.ReadEntry("Object 1/mimetype");
        string mimeText = System.Text.Encoding.UTF8.GetString(mimeBytes).Trim();
        Assert.Equal("application/vnd.oasis.opendocument.chart", mimeText);

        // 3. 驗證 Object 1/content.xml 內容
        byte[] objContentBytes = package.ReadEntry("Object 1/content.xml");
        string objContentXml = System.Text.Encoding.UTF8.GetString(objContentBytes);

        Assert.Contains("chart:chart", objContentXml);
        Assert.Contains("chart:class=\"chart:line\"", objContentXml);
        Assert.Contains("table:cell-range-address=", objContentXml);
        Assert.Contains("Sheet1", objContentXml);
        Assert.Contains("A1", objContentXml);
        Assert.Contains("B10", objContentXml);
        Assert.Contains("銷售折線圖", objContentXml);
        Assert.Contains("chart:legend-position=\"end\"", objContentXml);
        Assert.Contains("chart:plot-area", objContentXml);
        Assert.Contains("chart:axis", objContentXml);
    }

    /// <summary>
    /// 驗證資料驗證 (AddDataValidation) API 的全域宣告與儲存格關聯。
    /// </summary>
    [Fact]
    public void AddDataValidationAppliesCorrectRules()
    {
        using var document = SpreadsheetDocument.Create();
        document.AddSheet("Sheet1");

        var validation = new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(0, 0, 9, 0, "Sheet1"), // A1:A10
            Condition = OdfValidationCondition.IntegerBetween,
            Formula1 = "1",
            Formula2 = "100",
            ErrorMessage = "請輸入 1 至 100 的整數！",
            ErrorTitle = "無效輸入",
            AlertStyle = OdfValidationAlertStyle.Stop
        };

        document.AddDataValidation("Sheet1", validation);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        // 1. 驗證全域的 content-validations 宣告與條件
        Assert.Contains("table:content-validations", contentXml);
        Assert.Contains("table:content-validation", contentXml);
        Assert.Contains("table:name=\"val_1\"", contentXml);
        Assert.Contains("condition=\"and:oooc:isInteger()and:oooc:isBetween(1,100)\"", contentXml);
        Assert.Contains("table:error-message table:message=\"請輸入 1 至 100 的整數！\" table:title=\"無效輸入\" table:message-type=\"stop\"", contentXml);

        // 2. 驗證 A1 儲存格已正確被附加了此驗證名稱
        // 在 ODS 中列與欄均被展開，A1 為第 1 列 (row) 的第 1 個 cell
        Assert.Contains("table:content-validation-name=\"val_1\"", contentXml);
    }

    /// <summary>
    /// 驗證儲存格超連結 SetHyperlink / GetHyperlinkUrl / RemoveHyperlink API。
    /// </summary>
    [Fact]
    public void CellHyperlinkApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 1. SetHyperlink 帶顯示文字
        var cell = sheet.GetCell(0, 0);
        cell.SetHyperlink("https://example.com", "範例網站");
        Assert.Equal("https://example.com", cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);
        Assert.Equal("string", cell.ValueType);

        // 2. SetHyperlink 不帶顯示文字 — 使用 URL 本身
        var cell2 = sheet.GetCell(0, 1);
        cell2.SetHyperlink("https://odfkit.dev");
        Assert.Equal("https://odfkit.dev", cell2.GetHyperlinkUrl());
        Assert.Equal("https://odfkit.dev", cell2.DisplayText);

        // 3. 覆蓋既有超連結 URL，保留顯示文字
        cell.SetHyperlink("https://new.example.com");
        Assert.Equal("https://new.example.com", cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);

        // 4. RemoveHyperlink — 保留顯示文字，移除 text:a
        cell.RemoveHyperlink();
        Assert.Null(cell.GetHyperlinkUrl());
        Assert.Equal("範例網站", cell.DisplayText);

        // 5. GetHyperlinkUrl on plain cell returns null
        var cell3 = sheet.GetCell(0, 2);
        cell3.SetValue("普通文字");
        Assert.Null(cell3.GetHyperlinkUrl());

        // 6. XML 結構驗證
        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new System.IO.StreamReader(contentStream);
        string xml = reader.ReadToEnd();
        Assert.Contains("xlink:href=\"https://odfkit.dev\"", xml);
        Assert.Contains("xlink:type=\"simple\"", xml);
        Assert.Contains("text:a", xml);
    }

    /// <summary>
    /// 驗證儲存格批注 SetAnnotation / GetAnnotation / RemoveAnnotation API。
    /// </summary>
    [Fact]
    public void CellAnnotationApiWorksCorrectly()
    {
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.AddSheet("Sheet1");

        // 1. SetAnnotation 帶作者
        var cell = sheet.GetCell(0, 0);
        cell.SetAnnotation("這是批注", "Alice");
        var ann = cell.GetAnnotation();
        Assert.NotNull(ann);
        Assert.Equal("這是批注", ann.Text);
        Assert.Equal("Alice", ann.Author);
        Assert.False(ann.Visible);

        // 2. 覆蓋批注
        cell.SetAnnotation("新批注", visible: true);
        ann = cell.GetAnnotation();
        Assert.Equal("新批注", ann!.Text);
        Assert.True(ann.Visible);

        // 3. RemoveAnnotation
        cell.RemoveAnnotation();
        Assert.Null(cell.GetAnnotation());

        // 4. 無批注的儲存格回傳 null
        var cell2 = sheet.GetCell(0, 1);
        Assert.Null(cell2.GetAnnotation());

        // 5. XML 結構驗證
        cell.SetAnnotation("XML 驗證批注", "Bob");
        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new System.IO.StreamReader(contentStream);
        string xml = reader.ReadToEnd();
        Assert.Contains("office:annotation", xml);
        Assert.Contains("XML 驗證批注", xml);
        Assert.Contains("Bob", xml);
    }
}

using System;
using System.IO;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODT 轉換至 PDF 匯出 API。
/// </summary>
public class PdfExportTests
{
    /// <summary>
    /// 驗證含標題與段落的 ODT 可匯出為非空 PDF 位元組流。
    /// </summary>
    [Fact]
    public void Export_TextDocumentWithContent_ProducesNonEmptyPdfStream()
    {
        using var doc = TextDocument.Create();
        doc.AddHeading("PDF 測試標題", 1);
        doc.AddParagraph("這是一段測試內容，用來驗證 PDF 匯出功能。");
        doc.AddHeading("次標題", 2);
        doc.AddParagraph("第二段落。");

        using var ms = new MemoryStream();
        OdfPdfExporter.Export(doc, ms);

        Assert.True(ms.Length > 1024, "PDF 輸出不得小於 1 KB。");
        ms.Position = 0;
        byte[] header = new byte[5];
        ms.Read(header, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
    }

    /// <summary>
    /// 驗證空文件可匯出為 PDF 且不拋出例外。
    /// </summary>
    [Fact]
    public void Export_EmptyDocument_DoesNotThrow()
    {
        using var doc = TextDocument.Create();
        using var ms = new MemoryStream();
        var ex = Record.Exception(() => OdfPdfExporter.Export(doc, ms));
        Assert.Null(ex);
    }
}

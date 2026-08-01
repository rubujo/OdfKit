using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Drawing;
using OdfKit.Export;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證受管理匯出 facade 的一致 path、stream、async 與 report 契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class ExportFacadeTests
{
    /// <summary>
    /// 驗證 HTML、Markdown 與 SVG 共用相同的 stream report 形狀且不關閉呼叫端資料流。
    /// </summary>
    [Fact]
    public async Task ManagedTextAndSvgStreamExportsShareReportContract()
    {
        using TextDocument text = TextDocument.Create();
        text.AddParagraph("Export facade");
        using var html = new MemoryStream();
        using var markdown = new MemoryStream();

        OdfExportReport htmlReport = await OdfHtmlExporter.ExportToStreamAsync(
            text, html, null, TestContext.Current.CancellationToken);
        OdfExportReport markdownReport = OdfMarkdownExporter.ExportToStream(text, markdown, null);

        using DrawingDocument drawing = DrawingDocument.Create();
        OdfDrawPage page = drawing.Pages.Add("Canvas");
        page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(1));
        using var svg = new MemoryStream();
        OdfExportReport svgReport = await OdfSvgExporter.ExportToStreamAsync(
            drawing, svg, null, TestContext.Current.CancellationToken);

        Assert.Equal(OdfExportFormat.Html, htmlReport.Format);
        Assert.Equal(OdfExportFormat.Markdown, markdownReport.Format);
        Assert.Equal(OdfExportFormat.Svg, svgReport.Format);
        Assert.True(htmlReport.BytesWritten > 0);
        Assert.True(markdownReport.BytesWritten > 0);
        Assert.True(svgReport.BytesWritten > 0);
        Assert.True(html.CanWrite);
        Assert.Contains("Export facade", Encoding.UTF8.GetString(html.ToArray()), StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證預先取消的非同步路徑匯出不會截斷既有檔案。
    /// </summary>
    [Fact]
    public async Task ManagedPathExportPreCancellationDoesNotTruncateExistingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"odfkit-export-{Guid.NewGuid():N}.html");
        byte[] original = Encoding.UTF8.GetBytes("existing content");
        await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);

        try
        {
            using TextDocument text = TextDocument.Create();
            text.AddParagraph("replacement");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                OdfHtmlExporter.ExportToPathAsync(text, path, null, cancellation.Token));

            Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 驗證 PDF async stream facade 產生具 backend 與位元組數的報告。
    /// </summary>
    [Fact]
    public async Task PdfAsyncStreamExportReturnsManagedReport()
    {
        using TextDocument text = TextDocument.Create();
        text.AddParagraph("PDF facade");
        using var output = new MemoryStream();

        OdfExportReport report = await OdfPdfExporter.ExportToStreamAsync(
            text, output, TestContext.Current.CancellationToken);

        Assert.Equal(OdfExportFormat.Pdf, report.Format);
        Assert.Equal("managed-pdf", report.Backend);
        Assert.Equal(output.Length, report.BytesWritten);
        Assert.True(output.CanWrite);
    }
}

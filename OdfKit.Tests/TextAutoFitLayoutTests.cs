using System;
using System.IO;
using System.Threading;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Extensions.Imaging;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證跨格式文字 AutoFit 的 Unicode、效能預算與安全降級契約。
/// </summary>
public class TextAutoFitLayoutTests
{
    /// <summary>
    /// 驗證 Fast 模式會區分比例拉丁字寬、正確處理 emoji，並受欄寬上下限約束。
    /// </summary>
    [Fact]
    public void SpreadsheetFastAutoFitUsesUnicodeAndResolvedFontSize()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Layout");
        sheet.Cells[0, 0].SetValue("iiiiiiii😀");
        sheet.Cells[0, 1].SetValue("WWWWWWWW😀");
        sheet.Cells[0, 1].Style.Font.Size = "20pt";

        IReadOnlyDictionary<int, OdfLength> widths = sheet.AutoFitColumnWidths(
            [0, 1],
            new OdfAutoFitOptions(),
            TestContext.Current.CancellationToken);

        Assert.True(widths[1].ToCentimeters() > widths[0].ToCentimeters());
        Assert.InRange(widths[0].ToCentimeters(), 1, 50);
        Assert.InRange(widths[1].ToCentimeters(), 1, 50);
    }

    /// <summary>
    /// 驗證確定性列高會依欄寬、明確換行與字型大小計算，且關閉閱讀器最佳列高。
    /// </summary>
    [Fact]
    public void SpreadsheetAutoFitRowHeightMeasuresWrappedContent()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Rows");
        sheet.Cells[0, 0].SetValue("第一行\n第二行\n第三行");
        sheet.Cells[0, 0].Style.Font.Size = "16pt";
        sheet.SetColumnWidth(0, OdfLength.FromCentimeters(1.5));

        OdfLength height = sheet.AutoFitRowHeight(
            0,
            new OdfAutoFitOptions(),
            TestContext.Current.CancellationToken);

        Assert.True(height.ToCentimeters() > 1.5);
        Assert.False(sheet.IsRowOptimalHeight(0));
        Assert.Equal(height, sheet.GetRowHeight(0));
    }

    /// <summary>
    /// 驗證 Reader 模式使用 ODF 原生最佳欄寬與列高屬性，不在核心解析字型。
    /// </summary>
    [Fact]
    public void SpreadsheetReaderModeWritesNativeOptimalProperties()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Reader");
        sheet.Cells[0, 0].SetValue("reader");
        var options = new OdfAutoFitOptions { Mode = OdfAutoFitMode.Reader };

        _ = sheet.AutoFitColumnWidth(
            0,
            options,
            TestContext.Current.CancellationToken);
        _ = sheet.AutoFitRowHeight(
            0,
            options,
            TestContext.Current.CancellationToken);

        OdfNode column = OdfTableSheetDomAccessEngine.TryFindColumnNode(
            sheet.TableNode,
            0)!;
        OdfNode row = OdfTableSheetDomAccessEngine.TryFindRowNode(
            sheet.TableNode,
            0)!;
        string columnStyle = column.GetAttribute(
            "style-name",
            OdfNamespaces.Table)!;
        string rowStyle = row.GetAttribute(
            "style-name",
            OdfNamespaces.Table)!;
        Assert.Equal(
            "true",
            document.StyleEngine.GetStyleProperty(
                columnStyle,
                "use-optimal-column-width",
                OdfNamespaces.Style,
                "table-column"));
        Assert.Equal(
            "true",
            document.StyleEngine.GetStyleProperty(
                rowStyle,
                "use-optimal-row-height",
                OdfNamespaces.Style,
                "table-row"));
        Assert.Null(sheet.GetColumnWidth(0));
        Assert.Null(sheet.GetRowHeight(0));
    }

    /// <summary>
    /// 驗證 AutoFit 的儲存格、文字與取消預算會在超限時停止作業。
    /// </summary>
    [Fact]
    public void SpreadsheetAutoFitEnforcesBudgetsAndCancellation()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("Budget");
        sheet.Cells[0, 0].SetValue("first");
        sheet.Cells[1, 0].SetValue("second");

        Assert.Throws<InvalidOperationException>(() =>
            sheet.AutoFitColumnWidth(
                0,
                new OdfAutoFitOptions { MaximumCells = 1 },
                TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() =>
            sheet.AutoFitColumnWidth(
                0,
                new OdfAutoFitOptions
                {
                    MaximumTextElements = 3,
                    MaximumTextElementsPerBlock = 3
                },
                TestContext.Current.CancellationToken));

        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            sheet.AutoFitColumnWidth(
                0,
                new OdfAutoFitOptions(),
                source.Token));
    }

    /// <summary>
    /// 驗證精確量測工作階段可重用字型資源並計算換行後的實體高度。
    /// </summary>
    [Fact]
    public void PreciseSessionMeasuresWidthAndWrappedHeight()
    {
        using var session = new OdfTextLayoutSession(
            OdfFontContext.Default,
            maximumFonts: 2,
            maximumFontBytes: 32L * 1024 * 1024,
            maximumMeasurementCacheEntries: 8);
        var request = new OdfTextMeasureRequest
        {
            Text = "WWWW WWWW WWWW",
            FontFamily = "Arial",
            FontSizePoints = 12,
            AvailableWidthCentimeters = 1,
            Wrap = true
        };

        OdfTextMeasureResult first = session.Measure(
            request,
            TestContext.Current.CancellationToken);
        OdfTextMeasureResult second = session.Measure(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsExact);
        Assert.True(first.LineCount > 1);
        Assert.True(first.HeightCentimeters > 0);
        Assert.Equal(first.WidthCentimeters, second.WidthCentimeters);
        Assert.Equal(first.HeightCentimeters, second.HeightCentimeters);
    }

    /// <summary>
    /// 驗證精確量測不會讀入超過上限的字型檔，且文字預算與釋放狀態都會被強制執行。
    /// </summary>
    [Fact]
    public void PreciseSessionEnforcesFontTextAndLifetimeLimits()
    {
        string fontPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(fontPath, new byte[64]);
            var fontContext = new OdfFontContext();
            fontContext.RegisterFont("Oversized-Test-Font", fontPath);
            var session = new OdfTextLayoutSession(
                fontContext,
                maximumFonts: 1,
                maximumFontBytes: 8,
                maximumMeasurementCacheEntries: 8);
            var request = new OdfTextMeasureRequest
            {
                Text = "safe fallback",
                FontFamily = "Oversized-Test-Font",
                MaximumTextElements = 32
            };

            OdfTextMeasureResult result = session.Measure(
                request,
                TestContext.Current.CancellationToken);
            Assert.True(result.WidthCentimeters > 0);
            Assert.Throws<InvalidOperationException>(() =>
                session.Measure(
                    new OdfTextMeasureRequest
                    {
                        Text = "safe fallback",
                        FontFamily = "Oversized-Test-Font",
                        MaximumTextElements = 1
                    },
                    TestContext.Current.CancellationToken));

            session.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
                session.Measure(
                    request,
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(fontPath);
        }
    }

    /// <summary>
    /// 驗證 ODP 與 ODG 文字框共用 Fast 版面引擎並寫回實體高度。
    /// </summary>
    [Fact]
    public void PresentationAndDrawingTextBoxesShareAutoFitLayout()
    {
        var options = new OdfAutoFitOptions
        {
            ResizeTextBoxHeight = true,
            ResizeTextBoxWidth = false
        };

        using PresentationDocument presentation = PresentationDocument.Create();
        OdfTextBox slideText = presentation.Slides.Add("Slide").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(0.2),
            "第一行很長需要換行\n第二行");
        OdfTextMeasureResult slideResult = slideText.AutoFit(
            options,
            TestContext.Current.CancellationToken);

        using DrawingDocument drawing = DrawingDocument.Create();
        OdfTextBox drawingText = drawing.Pages.Add("Page").AddTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(0.2),
            "Drawing text that needs wrapping");
        OdfTextMeasureResult drawingResult = drawingText.AutoFit(
            options,
            TestContext.Current.CancellationToken);

        Assert.True(slideResult.HeightCentimeters > 0.2);
        Assert.True(drawingResult.HeightCentimeters > 0.2);
        Assert.NotEqual(
            "0.2cm",
            slideText.Node.GetAttribute("height", OdfNamespaces.Svg));
        Assert.NotEqual(
            "0.2cm",
            drawingText.Node.GetAttribute("height", OdfNamespaces.Svg));

        _ = drawingText.AutoFit(
            new OdfAutoFitOptions
            {
                Mode = OdfAutoFitMode.Reader,
                ResizeTextBoxWidth = true,
                ResizeTextBoxHeight = false
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "true",
            drawing.StyleEngine.GetStyleProperty(
                drawingText.Node.GetAttribute(
                    "style-name",
                    OdfNamespaces.Draw)!,
                "auto-grow-width",
                OdfNamespaces.Draw,
                "graphic"));
    }

    /// <summary>
    /// 驗證 ODT 浮動文字框可使用共用 Reader 與 Fast 配置策略。
    /// </summary>
    [Fact]
    public void TextDocumentFloatingTextBoxSupportsReaderAndFastAutoFit()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        OdfFloatingTextBox textBox = paragraph.AddFloatingTextBox(
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(1),
            OdfLength.FromCentimeters(2),
            OdfLength.FromCentimeters(0.2));
        _ = textBox.AddParagraph("ODT 文字框內容需要自動換行與增高");

        _ = textBox.AutoFit(
            new OdfAutoFitOptions
            {
                Mode = OdfAutoFitMode.Reader,
                ResizeTextBoxHeight = true
            },
            TestContext.Current.CancellationToken);
        OdfNode frame = textBox.TextBoxNode.Parent!;
        string readerStyle = frame.GetAttribute(
            "style-name",
            OdfNamespaces.Draw)!;
        Assert.Equal(
            "true",
            document.StyleEngine.GetStyleProperty(
                readerStyle,
                "auto-grow-height",
                OdfNamespaces.Draw,
                "graphic"));

        OdfTextMeasureResult result = textBox.AutoFit(
            new OdfAutoFitOptions
            {
                ResizeTextBoxHeight = true,
                ResizeTextBoxWidth = false
            },
            TestContext.Current.CancellationToken);
        Assert.True(result.HeightCentimeters > 0.2);
        Assert.Equal(
            "false",
            document.StyleEngine.GetStyleProperty(
                frame.GetAttribute("style-name", OdfNamespaces.Draw)!,
                "auto-grow-height",
                OdfNamespaces.Draw,
                "graphic"));
    }
}

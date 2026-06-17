using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OdfKit.Chart;
using OdfKit.Conversion;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;
using OdfSpreadsheetDocument = OdfKit.Spreadsheet.SpreadsheetDocument;

namespace OdfKit.Tests;

/// <summary>
/// 使用本機 Office 與 LibreOffice 驗證 OOXML 轉換結果可實機載入與匯出。
/// </summary>
public sealed class OfficeInteropConversionTests
{
    private const int WordPdfFormat = 17;
    private const int ExcelPdfFormat = 0;

    /// <summary>
    /// 驗證 ODT → DOCX 結果可由 Microsoft Word 開啟並匯出 PDF。
    /// </summary>
    [Fact]
    public void WordAndLibreOffice_RenderConvertedDocxToPdf()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Office COM 僅支援 Windows，略過 Word 實機驗收。");
        }

#pragma warning disable CA1416
        Type? wordType = Type.GetTypeFromProgID("Word.Application");
#pragma warning restore CA1416
        if (wordType is null)
        {
            Assert.Skip("找不到 Microsoft Word COM，略過 Word 實機驗收。");
        }

        string sofficePath = FindLibreOffice26Soffice();
        string tempRoot = CreateTempRoot();
        try
        {
            string odtPath = Path.Combine(tempRoot, "source.odt");
            string docxPath = Path.Combine(tempRoot, "converted.docx");
            string libreOfficePdfPath = Path.Combine(tempRoot, "source.pdf");
            string wordPdfPath = Path.Combine(tempRoot, "converted-word.pdf");

            using (TextDocument document = CreateTextSample())
            {
                document.Save(odtPath);
                using var docxStream = File.Create(docxPath);
                OdfToDocxConverter.Convert(document, docxStream);
            }

            RunSoffice(sofficePath, tempRoot, "pdf", odtPath);
            ExportWordPdf(wordType, docxPath, wordPdfPath);

            AssertPdfExists(libreOfficePdfPath);
            AssertPdfExists(wordPdfPath);
            AssertPdfVisualDifferenceBelow(libreOfficePdfPath, wordPdfPath, 5d);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    /// <summary>
    /// 驗證 ODS → XLSX 結果可由 Microsoft Excel 開啟並匯出 PDF。
    /// </summary>
    [Fact]
    public void ExcelAndLibreOffice_RenderConvertedXlsxToPdf()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Office COM 僅支援 Windows，略過 Excel 實機驗收。");
        }

#pragma warning disable CA1416
        Type? excelType = Type.GetTypeFromProgID("Excel.Application");
#pragma warning restore CA1416
        if (excelType is null)
        {
            Assert.Skip("找不到 Microsoft Excel COM，略過 Excel 實機驗收。");
        }

        string sofficePath = FindLibreOffice26Soffice();
        string tempRoot = CreateTempRoot();
        try
        {
            string odsPath = Path.Combine(tempRoot, "source.ods");
            string xlsxPath = Path.Combine(tempRoot, "converted.xlsx");
            string libreOfficePdfPath = Path.Combine(tempRoot, "source.pdf");
            string excelPdfPath = Path.Combine(tempRoot, "converted-excel.pdf");

            using (OdfSpreadsheetDocument document = CreateSpreadsheetSample())
            {
                document.Save(odsPath);
                using var xlsxStream = File.Create(xlsxPath);
                OdfToXlsxConverter.Convert(document, xlsxStream);
            }

            RunSoffice(sofficePath, tempRoot, "pdf", odsPath);
            ExportExcelPdf(excelType, xlsxPath, excelPdfPath);

            AssertPdfExists(libreOfficePdfPath);
            AssertPdfExists(excelPdfPath);
            AssertPdfVisualDifferenceBelow(libreOfficePdfPath, excelPdfPath, 5d);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static TextDocument CreateTextSample()
    {
        TextDocument document = TextDocument.Create();
        var setup = document.GetDefaultPageSetup();
        setup.HeaderText = "Q-1 實機頁首";
        setup.FooterText = "Q-1 實機頁尾";
        document.AddHeading("Q-1 實機標題", 1);
        var paragraph = document.AddParagraph();
        paragraph.AddTextRun("一般文字 ");
        var styledRun = paragraph.AddTextRun("粗斜線格式");
        styledRun.IsBold = true;
        styledRun.IsItalic = true;
        styledRun.IsUnderline = true;
        styledRun.FontSize = "14pt";

        var table = document.AddTable(2, 2);
        table.GetCell(0, 0).AddParagraph("A1");
        table.GetCell(0, 1).AddParagraph("B1");
        table.GetCell(1, 0).AddParagraph("A2");
        table.GetCell(1, 1).AddParagraph("B2");
        return document;
    }

    private static OdfSpreadsheetDocument CreateSpreadsheetSample()
    {
        OdfSpreadsheetDocument document = OdfSpreadsheetDocument.Create();
        var sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;
        sheet.Cells["B4"].Formula = "of:=SUM(B2:B3)";
        sheet.Cells["B4"].CellValue = 280d;

        document.AddChart("Data", new OdfCellAddress(5, 0, "Data"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "Q-2 實機圖表",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data")
        });

        document.AddDataValidation("Data", new OdfDataValidation
        {
            ApplyTo = new OdfCellRange(1, 1, 2, 1, "Data"),
            Condition = OdfValidationCondition.DecimalBetween,
            Formula1 = "0",
            Formula2 = "1000"
        });
        return document;
    }

    private static void ExportWordPdf(Type wordType, string docxPath, string pdfPath)
    {
        dynamic? word = null;
        dynamic? documents = null;
        dynamic? document = null;
        try
        {
            word = Activator.CreateInstance(wordType);
            if (word is null)
            {
                throw new InvalidOperationException("無法啟動 Microsoft Word。");
            }

            word.Visible = false;
            word.DisplayAlerts = 0;
            documents = word.Documents;
            document = documents.Open(docxPath, ReadOnly: true, AddToRecentFiles: false, Visible: false);
            if (document.Tables.Count < 1)
            {
                throw new InvalidOperationException("Word 未載入預期表格。");
            }

            document.ExportAsFixedFormat(pdfPath, WordPdfFormat);
        }
        finally
        {
            try
            {
                document?.Close(false);
            }
            finally
            {
                word?.Quit(false);
                ReleaseComObject(document);
                ReleaseComObject(documents);
                ReleaseComObject(word);
                CollectComReferences();
            }
        }
    }

    private static void ExportExcelPdf(Type excelType, string xlsxPath, string pdfPath)
    {
        dynamic? excel = null;
        dynamic? workbooks = null;
        dynamic? workbook = null;
        dynamic? worksheets = null;
        dynamic? worksheet = null;
        dynamic? range = null;
        try
        {
            excel = Activator.CreateInstance(excelType);
            if (excel is null)
            {
                throw new InvalidOperationException("無法啟動 Microsoft Excel。");
            }

            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbooks = excel.Workbooks;
            workbook = workbooks.Open(xlsxPath, 0, true);
            worksheets = workbook.Worksheets;
            worksheet = worksheets.Item[1];
            range = worksheet.Range("B4");
            string formula = range.Formula;
            if (!formula.Contains("SUM", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Excel 未載入預期公式。");
            }

            workbook.ExportAsFixedFormat(ExcelPdfFormat, pdfPath);
        }
        finally
        {
            try
            {
                workbook?.Close(false);
            }
            finally
            {
                excel?.Quit();
                ReleaseComObject(range);
                ReleaseComObject(worksheet);
                ReleaseComObject(worksheets);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excel);
                CollectComReferences();
            }
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
#pragma warning disable CA1416
            Marshal.FinalReleaseComObject(instance);
#pragma warning restore CA1416
        }
    }

    private static void CollectComReferences()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static string FindLibreOffice26Soffice()
    {
        string? configured = Environment.GetEnvironmentVariable("ODFKIT_SOFFICE_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            Assert.Skip("未設定 ODFKIT_SOFFICE_PATH，略過 LibreOffice 實機 PDF 驗收。");
        }

        string? executable = ResolveSofficeExecutable(configured!);
        if (string.IsNullOrEmpty(executable))
        {
            Assert.Skip("ODFKIT_SOFFICE_PATH 未指向可執行的 soffice。");
        }

        string version = GetVersion(executable!);
        if (!version.Contains("LibreOffice 26.", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("ODFKIT_SOFFICE_PATH 不是 LibreOffice 26.x。");
        }

        return executable!;
    }

    private static string? ResolveSofficeExecutable(string candidate)
    {
        if (File.Exists(candidate))
        {
            if (Path.GetExtension(candidate).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string consoleCandidate = Path.ChangeExtension(candidate, ".com");
                if (File.Exists(consoleCandidate) && !string.IsNullOrWhiteSpace(GetVersion(consoleCandidate)))
                {
                    return consoleCandidate;
                }
            }

            return candidate;
        }

        if (!Directory.Exists(candidate))
        {
            return null;
        }

        string[] candidates =
        [
            Path.Combine(candidate, "soffice.com"),
            Path.Combine(candidate, "soffice.exe"),
            Path.Combine(candidate, "program", "soffice.com"),
            Path.Combine(candidate, "program", "soffice.exe")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetVersion(string sofficePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--version");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 soffice。");
        process.WaitForExit(10_000);
        return process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    }

    private static void RunSoffice(string sofficePath, string outputDir, string targetFormat, string inputPath)
    {
        string profileDir = Path.Combine(outputDir, "lo-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(profileDir + Path.DirectorySeparatorChar).AbsoluteUri);
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(targetFormat);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDir);
        startInfo.ArgumentList.Add(inputPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 soffice。");
        Assert.True(process.WaitForExit(60_000), "LibreOffice 轉換逾時。");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, $"LibreOffice 轉換失敗，輸出：{output}");
    }

    private static void AssertPdfExists(string path)
    {
        Assert.True(File.Exists(path), $"應輸出 PDF：{path}");
        Assert.True(new FileInfo(path).Length > 1024, $"PDF 不應為空：{path}");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4];
        Assert.Equal(4, stream.Read(header));
        Assert.True(header.SequenceEqual("%PDF"u8), $"檔案不是 PDF：{path}");
    }

    private static void AssertPdfVisualDifferenceBelow(string expectedPdfPath, string actualPdfPath, double thresholdPercent)
    {
        string pythonPath = FindPdfRendererPython();
        string scriptPath = ResolvePdfVisualDiffScriptPath();

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add(expectedPdfPath);
        startInfo.ArgumentList.Add(actualPdfPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 PDF 視覺差異比對。");
        Assert.True(process.WaitForExit(60_000), "PDF 視覺差異比對逾時。");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        Assert.True(process.ExitCode == 0, $"PDF 視覺差異比對失敗：{error}");
        Assert.True(double.TryParse(output, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double differencePercent),
            "PDF 視覺差異比對輸出不是數值：" + output);
        Assert.True(differencePercent <= thresholdPercent,
            $"PDF 視覺差異 {differencePercent:F2}% 超過 {thresholdPercent:F2}% 門檻。");
    }

    private static string ResolvePdfVisualDiffScriptPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "eng", "scripts", "PdfVisualDiff.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                break;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 eng/scripts/PdfVisualDiff.py。");
    }

    private static string FindPdfRendererPython()
    {
        string? configured = Environment.GetEnvironmentVariable("ODFKIT_PDF_RENDERER_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        Assert.Skip("未設定 ODFKIT_PDF_RENDERER_PYTHON，略過 PDF 像素級視覺差異驗收。");
        return string.Empty;
    }

    private static string CreateTempRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitOfficeInterop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("ODFKIT_KEEP_INTEROP_ARTIFACTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 使用真實 LibreOffice 26.x binary 驗證 OdfKit 產生文件的互通性。
/// </summary>
public class LibreOfficeInteropTests
{
    /// <summary>
    /// 驗證含追蹤修訂的 ODT 可由 LibreOffice 26.x headless 模式載入、轉換並由 OdfKit 重新讀取。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsTrackedChangesOdt()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過追蹤修訂實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeTrackedChanges_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-tracked-changes.odt");
            CreateTrackedChangesDocument(odtPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-tracked-changes.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出追蹤修訂 ODT 的文字轉換結果。");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string txt;
            using (var stream = File.OpenRead(txtPath))
            {
                try
                {
                    var utf8Throw = System.Text.Encoding.GetEncoding("utf-8", System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);
                    using (var reader = new StreamReader(stream, utf8Throw, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
                catch (DecoderFallbackException)
                {
                    stream.Position = 0;
                    int codePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                    using (var reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(codePage), detectEncodingFromByteOrderMarks: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
            }
            Assert.Contains("OdfKit-TrackedChanges-Marker", txt);
            Assert.Contains("表格追蹤修訂", txt);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odt", odtPath);
            string roundTripPath = Path.Combine(outputDir, "interop-tracked-changes.odt");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出追蹤修訂 ODT 往返結果。");

            using TextDocument document = TextDocument.Load(roundTripPath);
            string contentXml = ReadContentXml(document);
            Assert.Contains("OdfKit-TrackedChanges-Marker", contentXml);
            Assert.Contains("表格追蹤修訂", contentXml);

            var changes = document.GetTrackedChanges().ToList();
            if (changes.Count > 0)
            {
                Assert.Contains(changes, change => change.ChangeType == OdfChangeType.Insertion);
                document.AcceptAllChanges();
                Assert.Empty(document.GetTrackedChanges());
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證含 <c>table:tracked-changes</c> 的 ODS 可由 LibreOffice 26.x headless 模式載入並往返。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsTrackedChangesOds()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過 ODS 追蹤修訂實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeTrackedChangesOds_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-tracked-changes.ods");
            CreateTrackedChangesSpreadsheet(odsPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-tracked-changes.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出追蹤修訂 ODS 往返結果。");

            using SpreadsheetDocument document = SpreadsheetDocument.Load(roundTripPath);
            string contentXml = ReadSpreadsheetContentXml(document);
            Assert.Contains("OdfKit-TrackedChanges-Ods-Marker", contentXml);
            Assert.Contains("table:tracked-changes", contentXml);

            if (document.GetTrackedChanges().Count > 0)
            {
                document.AcceptAllChanges();
                Assert.Empty(document.GetTrackedChanges());
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 ODT、ODS、ODP 與 ODG 可由 LibreOffice 26.x headless 模式載入並轉換。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsGeneratedDocuments()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-text.odt");
            string odsPath = Path.Combine(tempRoot, "interop-chart.ods");
            string odpPath = Path.Combine(tempRoot, "interop-animation.odp");
            string odgPath = Path.Combine(tempRoot, "interop-drawing.odg");

            CreateTextDocument(odtPath);
            CreateSpreadsheetWithChart(odsPath);
            CreatePresentationWithAnimation(odpPath);
            CreateDrawingDocument(odgPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-text.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出 ODT 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-26-Interop-Marker", File.ReadAllText(txtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "xlsx", odsPath);
            string xlsxPath = Path.Combine(outputDir, "interop-chart.xlsx");
            Assert.True(File.Exists(xlsxPath), "LibreOffice 應輸出 ODS 至 XLSX 轉換結果。");
            Assert.True(new FileInfo(xlsxPath).Length > 0, "XLSX 轉換結果不應為空。");

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodp", odpPath);
            string fodpPath = Path.Combine(outputDir, "interop-animation.fodp");
            Assert.True(File.Exists(fodpPath), "LibreOffice 應輸出 ODP 至 FODP 轉換結果。");
            string fodpXml = File.ReadAllText(fodpPath);
            Assert.Contains("ooo-entrance-fade-in", fodpXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodg", odgPath);
            string fodgPath = Path.Combine(outputDir, "interop-drawing.fodg");
            Assert.True(File.Exists(fodgPath), "LibreOffice 應輸出 ODG 至 FODG 轉換結果。");
            string fodgXml = File.ReadAllText(fodgPath);
            Assert.Contains("OdfKit-LibreOffice-26-Interop-Marker", fodgXml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateTextDocument(string path)
    {
        using var document = TextDocument.Create();
        document.AddHeading("LibreOffice 26 互通性", 1);
        document.AddParagraph("LibreOffice 26 互通性文字");
        document.AddParagraph("OdfKit-LibreOffice-26-Interop-Marker");
        document.Save(path);
    }

    private static void CreateTrackedChangesDocument(string path)
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;
        document.AddParagraph("OdfKit-TrackedChanges-Marker");

        OdfTable table = document.AddTable(1, 1);
        table.GetCell(0, 0).AddParagraph(string.Empty).AddTextRun("表格追蹤修訂");
        document.Save(path);
    }

    private static void CreateTrackedChangesSpreadsheet(string path)
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "OdfKit-TrackedChanges-Ods-Marker";
        sheet.Cells["A2"].CellValue = 100d;
        sheet.Cells["B2"].CellValue = 200d;
        sheet.Cells["C2"].Formula = "of:=[.A2]+[.B2]";

        document.TrackedChanges = true;
        sheet.Cells["C2"].Formula = "of:=[.A2]*[.B2]";
        document.Save(path);
    }

    private static string ReadSpreadsheetContentXml(SpreadsheetDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CreateSpreadsheetWithChart(string path)
    {
        using var document = SpreadsheetDocument.Create();
        var sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;

        document.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "LibreOffice 26 圖表標題",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
            HasLegend = true
        });
        document.Save(path);
    }

    private static void CreatePresentationWithAnimation(string path)
    {
        using var document = PresentationDocument.Create();
        var slide = document.AddSlide();
        var placeholder = slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"));
        slide.AddEntranceEffect(placeholder.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);
        document.Save(path);
    }

    private static void CreateDrawingDocument(string path)
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("互通頁");
        page.AddTextBox(
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("8cm"),
            OdfLength.Parse("3cm"),
            "OdfKit-LibreOffice-26-Interop-Marker");
        document.Save(path);
    }

    private static string? FindLibreOffice26Soffice()
    {
        foreach (string candidate in EnumerateSofficeCandidates())
        {
            string? executable = ResolveSofficeExecutable(candidate);
            if (string.IsNullOrEmpty(executable) || executable.Contains("MockSoffice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string version = GetVersion(executable);
            if (version.Contains("LibreOffice 26.", StringComparison.OrdinalIgnoreCase))
            {
                return executable;
            }
        }

        return null;
    }

    private static string[] EnumerateSofficeCandidates()
    {
        string[] environmentCandidates = ExpandEnvironmentCandidate(Environment.GetEnvironmentVariable("ODFKIT_SOFFICE_PATH"))
            .Concat(ExpandEnvironmentCandidate(Environment.GetEnvironmentVariable("LIBREOFFICE_PATH")))
            .ToArray();

        string[] wellKnownCandidates =
        [
            @"D:\Portable\PotableAppsPlatform\PortableApps\LibreOfficePortable\App\libreoffice\program\soffice.com",
            @"D:\Portable\PotableAppsPlatform\PortableApps\LibreOfficePortable\App\libreoffice\program\soffice.exe",
            @"C:\Program Files\LibreOffice\program\soffice.com",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.com",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        ];

        string? pathCandidate = FindOnPath("soffice");
        return [.. environmentCandidates, .. wellKnownCandidates, pathCandidate ?? string.Empty];
    }

    private static IEnumerable<string> ExpandEnvironmentCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        yield return value;

        if (!Directory.Exists(value))
        {
            yield break;
        }

        yield return Path.Combine(value, "soffice.com");
        yield return Path.Combine(value, "soffice.exe");
        yield return Path.Combine(value, "program", "soffice.com");
        yield return Path.Combine(value, "program", "soffice.exe");
    }

    private static string? FindOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string[] candidates = OperatingSystem.IsWindows()
                ? [Path.Combine(directory, fileName + ".com"), Path.Combine(directory, fileName + ".exe")]
                : [Path.Combine(directory, fileName)];
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
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

        return Directory.Exists(candidate)
            ? ExpandEnvironmentCandidate(candidate).FirstOrDefault(File.Exists)
            : null;
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

    private static void RunSoffice(string sofficePath, string userInstallationDir, string outputDir, string targetFormat, string inputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(userInstallationDir + Path.DirectorySeparatorChar).AbsoluteUri);
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
        Assert.DoesNotContain("Error", output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證官方範例 Sample.cs 可成功編譯執行，且其產生的 ODT、ODS、ODP 檔案可由 LibreOffice 完美載入與相容。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsSampleGeneratedDocuments()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過範例文件實機互通性測試。");
        }

        string slnRoot = FindSolutionRoot();
        string sampleOutput = Path.Combine(slnRoot, "samples", "output");

        // 1. 執行 dotnet run samples/Sample.cs
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = slnRoot
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("samples/Sample.cs");

        using (var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 dotnet run。"))
        {
            Assert.True(process.WaitForExit(90_000), "執行範例程式 Sample.cs 逾時。");
            string runOutput = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            Assert.True(process.ExitCode == 0, $"執行範例程式 Sample.cs 失敗，輸出：{runOutput}");
        }

        // 2. 驗證預期產出的 ODF 檔案存在
        string[] generatedFiles =
        [
            "output_text.odt",
            "output_spreadsheet.ods",
            "output_presentation.odp",
            "output_stream.ods",
            "output_stream.odt"
        ];

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitSampleInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            foreach (var fileName in generatedFiles)
            {
                string filePath = Path.Combine(sampleOutput, fileName);
                Assert.True(File.Exists(filePath), $"範例產出檔案不存在：{filePath}");

                // 3. 呼叫 LibreOffice 實機載入並轉為 PDF 以驗證相容性
                RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", filePath);
                string pdfName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";
                string pdfPath = Path.Combine(outputDir, pdfName);
                Assert.True(File.Exists(pdfPath), $"LibreOffice 應能成功將範例檔案 {fileName} 轉為 PDF。");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證由 OdtStreamWriter 與 OdsStreamWriter 流式寫入產生的文件可由 LibreOffice 完美載入與相容。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsStreamWriterGeneratedDocuments()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過流式寫入文件實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitStreamWriterInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            // 1. 流式寫入 ODT
            string odtPath = Path.Combine(tempRoot, "stream-text.odt");
            using (var fs = new FileStream(odtPath, FileMode.Create, FileAccess.Write))
            using (var writer = new OdtStreamWriter(fs))
            {
                writer.AddHeading("流式文字文件", 1);
                writer.AddParagraph("這是一個使用 OdtStreamWriter 產生的流式文件。");
            }

            // 2. 流式寫入 ODS
            string odsPath = Path.Combine(tempRoot, "stream-sheet.ods");
            using (var fs = new FileStream(odsPath, FileMode.Create, FileAccess.Write))
            using (var writer = new OdsStreamWriter(fs))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell("欄位一");
                writer.WriteCell(123.45d);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            // 3. 呼叫 LibreOffice 實機驗證 ODT
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odtPath);
            Assert.True(File.Exists(Path.Combine(outputDir, "stream-text.pdf")), "LibreOffice 應能成功將流式 ODT 轉為 PDF。");

            // 4. 呼叫 LibreOffice 實機驗證 ODS
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odsPath);
            Assert.True(File.Exists(Path.Combine(outputDir, "stream-sheet.pdf")), "LibreOffice 應能成功將流式 ODS 轉為 PDF。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 CJK 增補平面與自造字罕見字寫入文件後，其 LibreOffice 實機開檔與轉檔內容的字元保真度。
    /// </summary>
    [Fact]
    public void LibreOffice26Headless_LoadsSupplementaryPlaneFontDocument()
    {
        string? sofficePath = FindLibreOffice26Soffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 LibreOffice 26.x soffice binary，略過罕見字實機字元保真度測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitRareCharInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            using var doc = TextDocument.Create();
            var p = doc.Body.Paragraphs.Add();
            p.AddTextRun("罕見字字型對照保真度測試：吉、𠮷、𠜎、𿿽。");

            string odtPath = Path.Combine(tempRoot, "rare-chars.odt");
            doc.Save(odtPath);

            // 呼叫 LibreOffice 轉為純文字，強制以 UTF-8 輸出以防止罕見字遺失或轉為問號
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt:Text (encoded):UTF8", odtPath);
            string txtPath = Path.Combine(outputDir, "rare-chars.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應成功轉換為文字檔案。");

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string txt;
            using (var stream = File.OpenRead(txtPath))
            {
                try
                {
                    var utf8Throw = System.Text.Encoding.GetEncoding("utf-8", System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);
                    using (var reader = new StreamReader(stream, utf8Throw, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
                catch (DecoderFallbackException)
                {
                    stream.Position = 0;
                    // 若發生解碼失敗，退回至標準容錯 UTF-8 讀取（無效字元會解碼為 replacement character，但罕見字能被保留）
                    using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
            }

            Assert.Contains("𠮷", txt);
            Assert.Contains("𠜎", txt);
            Assert.Contains("𿿽", txt);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string FindSolutionRoot()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "OdfKit.slnx")) || Directory.Exists(Path.Combine(dir, "samples")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        throw new DirectoryNotFoundException("找不到專案方案根目錄。");
    }
}

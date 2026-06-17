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
            string txt = File.ReadAllText(txtPath);
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
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OdfKit.Chart;
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
    /// 驗證 ODT、ODS 與 ODP 可由 LibreOffice 26.x headless 模式載入並轉換。
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

            CreateTextDocument(odtPath);
            CreateSpreadsheetWithChart(odsPath);
            CreatePresentationWithAnimation(odpPath);

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

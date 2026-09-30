using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace OdfKit.Benchmarks;

/// <summary>
/// Standalone, single-shot timing runner for the competitive read comparison, the read-side counterpart of
/// <see cref="CompetitiveStreamWriteManualRunner"/>. Each scenario reads the same deterministic 1,000,000-row
/// dataset in its own child process, so peak working set reflects a single scenario, and the content read
/// back is verified against the generator with a <see cref="CompetitiveReadChecksum"/>.
/// 跨套件讀取對比的獨立單次計時執行器，是 <see cref="CompetitiveStreamWriteManualRunner"/> 的讀取端對應。
/// 每個情境在各自的子行程中讀取同一份決定性的一百萬列資料，使峰值工作集只反映單一情境，並以
/// <see cref="CompetitiveReadChecksum"/> 對照產生器驗證讀回的內容。
/// </summary>
internal static class CompetitiveStreamReadManualRunner
{
    private const string OdsScenario = "OdsStreamReader";
    private const string MiniExcelScenario = "MiniExcel";
    private const string ClosedXmlScenario = "ClosedXml";

    private static readonly string[] s_scenarios = [OdsScenario, MiniExcelScenario, ClosedXmlScenario];

    private sealed record ReadResult(
        string Scenario,
        double ElapsedMs,
        long AllocatedBytes,
        long InputLength,
        long PeakWorkingSetBytes,
        bool ChecksumMatches);

    /// <summary>
    /// Writes the input files, runs every read scenario in its own child process, verifies the checksums,
    /// and prints a Markdown result table.
    /// 寫出輸入檔、於各自的子行程執行所有讀取情境、驗證檢查碼，並印出 Markdown 結果表格。
    /// </summary>
    /// <returns>The process exit code (0 when every checksum matches). / 行程結束代碼（所有檢查碼相符時為 0）。</returns>
    internal static int RunOrchestrator()
    {
        Console.WriteLine(FormattableString.Invariant(
            $"讀取對比手動計時模式：{CompetitiveBenchmarkData.RowCount:N0} 列 x {CompetitiveBenchmarkData.ColumnCount} 欄，每個情境於獨立子行程執行一次。"));

        string directory = Path.Combine(Path.GetTempPath(), $"odfkit-competitive-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string odsPath = Path.Combine(directory, "dataset.ods");
            string xlsxPath = Path.Combine(directory, "dataset.xlsx");

            Console.WriteLine("準備輸入檔（.ods 由 OdsStreamWriter 寫入，.xlsx 由 MiniExcel 串流寫入）…");
            using (FileStream ods = File.Create(odsPath))
            {
                CompetitiveStreamWriters.WriteOdsStreamWriter(ods);
            }

            using (FileStream xlsx = File.Create(xlsxPath))
            {
                CompetitiveStreamWriters.WriteMiniExcel(xlsx);
            }

            Console.WriteLine("計算預期檢查碼（直接由產生器）…");
            CompetitiveReadChecksum expected = CompetitiveStreamReaders.ComputeExpected();
            Console.WriteLine();

            var results = new List<ReadResult>();
            foreach (string scenario in s_scenarios)
            {
                string inputPath = scenario == OdsScenario ? odsPath : xlsxPath;
                Console.WriteLine($"執行中：{scenario}…");
                ReadResult result = RunScenarioInChildProcess(scenario, inputPath, expected);
                results.Add(result);
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  耗時：{result.ElapsedMs:N0} ms，配置量：{result.AllocatedBytes / 1024.0 / 1024.0:N1} MB，峰值工作集：{FormatPeak(result.PeakWorkingSetBytes)}，檢查碼：{(result.ChecksumMatches ? "相符" : "不符")}"));
            }

            Console.WriteLine();
            Console.WriteLine("| 情境 | 輸入格式 | 輸入檔案大小 (MB) | 耗時 (ms) | 配置量 (MB) | 峰值工作集 (MB) | 檢查碼 |");
            Console.WriteLine("|------|----------|--------------------|-----------|-------------|------------------|--------|");
            foreach (ReadResult result in results)
            {
                string format = result.Scenario == OdsScenario ? ".ods" : ".xlsx";
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {result.Scenario} | {format} | {result.InputLength / 1024.0 / 1024.0:N1} | {result.ElapsedMs:N0} | {result.AllocatedBytes / 1024.0 / 1024.0:N1} | {FormatPeak(result.PeakWorkingSetBytes)} | {(result.ChecksumMatches ? "相符" : "不符")} |"));
            }

            if (results.Exists(result => !result.ChecksumMatches))
            {
                Console.Error.WriteLine("檢查碼不符：至少一個讀取器讀回的內容與產生器不一致，量測結果無效。");
                return 1;
            }

            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // 暫存檔清理失敗不應中斷量測流程。
            }
        }
    }

    /// <summary>
    /// Runs a single named read scenario in-process and prints a machine-readable result line.
    /// 在目前行程中執行單一指定的讀取情境，並印出機器可讀的結果行。
    /// </summary>
    /// <param name="scenario">The scenario name (<c>OdsStreamReader</c>, <c>MiniExcel</c>, or <c>ClosedXml</c>). / 情境名稱。</param>
    /// <param name="inputPath">The input file path. / 輸入檔案路徑。</param>
    /// <returns>The process exit code (0 on success). / 行程結束代碼（成功時為 0）。</returns>
    internal static int RunSingleScenario(string scenario, string inputPath)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        CompetitiveReadChecksum checksum;
        switch (scenario)
        {
            case OdsScenario:
                checksum = CompetitiveStreamReaders.ReadOdsStreamReader(inputPath);
                break;
            case MiniExcelScenario:
                checksum = CompetitiveStreamReaders.ReadMiniExcel(inputPath);
                break;
            case ClosedXmlScenario:
                checksum = CompetitiveStreamReaders.ReadClosedXml(inputPath);
                break;
            default:
                Console.Error.WriteLine($"未知情境：{scenario}");
                return 1;
        }

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Console.WriteLine(FormattableString.Invariant(
            $"RESULT|{scenario}|{stopwatch.Elapsed.TotalMilliseconds}|{allocated}|{checksum.Serialize()}"));
        return 0;
    }

    private static string FormatPeak(long bytes) =>
        bytes >= 0
            ? (bytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture)
            : "n/a";

    private static ReadResult RunScenarioInChildProcess(
        string scenario,
        string inputPath,
        CompetitiveReadChecksum expected)
    {
        string dllPath = typeof(CompetitiveStreamReadManualRunner).Assembly.Location;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(dllPath);
        startInfo.ArgumentList.Add("--run-read-single");
        startInfo.ArgumentList.Add(scenario);
        startInfo.ArgumentList.Add(inputPath);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動子行程。");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        // Windows 在子行程結束後即拒絕查詢 PeakWorkingSet64，因此必須在行程仍存活期間輪詢並保留最大值。
        long peakWorkingSetBytes = -1;
        while (!process.HasExited)
        {
            try
            {
                process.Refresh();
                peakWorkingSetBytes = Math.Max(peakWorkingSetBytes, process.PeakWorkingSet64);
            }
            catch (InvalidOperationException)
            {
                // 行程可能在 Refresh 與讀取之間結束，忽略並使用已知的最大值。
            }
            catch (PlatformNotSupportedException)
            {
                peakWorkingSetBytes = -1;
                break;
            }

            process.WaitForExit(50);
        }

        process.WaitForExit();
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"情境 {scenario} 子行程結束代碼為 {process.ExitCode}。stderr: {stderr}");
        }

        string? resultLine = null;
        foreach (string line in stdout.Split('\n'))
        {
            if (line.StartsWith("RESULT|", StringComparison.Ordinal))
            {
                resultLine = line.Trim();
                break;
            }
        }

        if (resultLine is null)
        {
            throw new InvalidOperationException($"情境 {scenario} 未回傳結果行。stdout: {stdout}");
        }

        string[] parts = resultLine.Split('|');
        double elapsedMs = double.Parse(parts[2], CultureInfo.InvariantCulture);
        long allocatedBytes = long.Parse(parts[3], CultureInfo.InvariantCulture);
        CompetitiveReadChecksum actual = CompetitiveReadChecksum.Parse(parts[4]);
        if (!expected.Matches(actual))
        {
            Console.Error.WriteLine($"  預期：{expected.Serialize()}");
            Console.Error.WriteLine($"  實際：{actual.Serialize()}");
        }

        return new ReadResult(
            scenario,
            elapsedMs,
            allocatedBytes,
            new FileInfo(inputPath).Length,
            peakWorkingSetBytes,
            expected.Matches(actual));
    }
}

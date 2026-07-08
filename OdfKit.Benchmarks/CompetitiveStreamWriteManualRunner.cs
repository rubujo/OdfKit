using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OdfKit.Benchmarks;

/// <summary>
/// Standalone, single-shot timing runner for <see cref="CompetitiveStreamWriteBenchmarks"/>, used as a
/// manual measurement mode when a full BenchmarkDotNet statistical run over 1,000,000-row scenarios is
/// impractical, and to additionally capture per-process peak working set (which BenchmarkDotNet's
/// <c>MemoryDiagnoser</c> does not report).
/// 供 <see cref="CompetitiveStreamWriteBenchmarks"/> 使用的獨立單次計時執行器；當一百萬列情境下執行完整
/// BenchmarkDotNet 統計量測不切實際時作為手動量測模式，並額外量測 BenchmarkDotNet 的
/// <c>MemoryDiagnoser</c> 未提供的每行程峰值工作集。
/// </summary>
/// <remarks>
/// Each scenario is executed in its own isolated child process (via <c>--run-single</c>) so that peak
/// working set reflects a single scenario rather than an accumulated value across scenarios run in the
/// same process.
/// 每個情境都在各自獨立的子行程中執行（透過 <c>--run-single</c>），使峰值工作集反映單一情境本身，
/// 而非同一行程內多個情境累加後的數值。
/// </remarks>
internal static class CompetitiveStreamWriteManualRunner
{
    private static readonly string[] s_scenarios = ["OdsStreamWriter", "MiniExcel", "ClosedXml"];

    /// <summary>
    /// Runs all competitive scenarios, each in its own child process, and prints a Markdown result table.
    /// 於各自的子行程中執行所有跨套件對比情境，並印出 Markdown 結果表格。
    /// </summary>
    /// <returns>The process exit code (0 on success). / 行程結束代碼（成功時為 0）。</returns>
    internal static int RunOrchestrator()
    {
        Console.WriteLine(FormattableString.Invariant($"手動計時模式：{CompetitiveBenchmarkData.RowCount:N0} 列 x {CompetitiveBenchmarkData.ColumnCount} 欄，每個情境於獨立子行程執行一次。"));
        Console.WriteLine();

        var results = new System.Collections.Generic.List<(string Scenario, double ElapsedMs, long AllocatedBytes, long FileLength, long PeakWorkingSetBytes)>();
        foreach (string scenario in s_scenarios)
        {
            Console.WriteLine($"執行中：{scenario}…");
            var result = RunScenarioInChildProcess(scenario);
            results.Add(result);
            string elapsedText = result.ElapsedMs.ToString("N0", CultureInfo.InvariantCulture);
            string allocatedText = (result.AllocatedBytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture);
            string peakText = result.PeakWorkingSetBytes >= 0
                ? (result.PeakWorkingSetBytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture) + " MB"
                : "不適用";
            string fileLengthText = (result.FileLength / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture);
            Console.WriteLine($"  耗時：{elapsedText} ms，配置量：{allocatedText} MB，峰值工作集：{peakText}，輸出檔案大小：{fileLengthText} MB");
        }

        Console.WriteLine();
        Console.WriteLine("| 情境 | 耗時 (ms) | 配置量 (MB) | 峰值工作集 (MB) | 輸出檔案大小 (MB) |");
        Console.WriteLine("|------|-----------|-------------|------------------|--------------------|");
        foreach (var result in results)
        {
            string peak = result.PeakWorkingSetBytes >= 0
                ? (result.PeakWorkingSetBytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture)
                : "n/a";
            Console.WriteLine(FormattableString.Invariant(
                $"| {result.Scenario} | {result.ElapsedMs:N0} | {result.AllocatedBytes / 1024.0 / 1024.0:N1} | {peak} | {result.FileLength / 1024.0 / 1024.0:N1} |"));
        }

        return 0;
    }

    /// <summary>
    /// Runs a single named scenario in-process and prints a machine-readable result line to standard output.
    /// 在目前行程中執行單一指定情境，並將機器可讀的結果行印至標準輸出。
    /// </summary>
    /// <param name="scenario">The scenario name (<c>OdsStreamWriter</c>, <c>MiniExcel</c>, or <c>ClosedXml</c>). / 情境名稱（<c>OdsStreamWriter</c>、<c>MiniExcel</c> 或 <c>ClosedXml</c>）。</param>
    /// <param name="outputPath">The output file path to write to. / 要寫入的輸出檔案路徑。</param>
    /// <returns>The process exit code (0 on success). / 行程結束代碼（成功時為 0）。</returns>
    internal static int RunSingleScenario(string scenario, string outputPath)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            switch (scenario)
            {
                case "OdsStreamWriter":
                    CompetitiveStreamWriters.WriteOdsStreamWriter(fileStream);
                    break;
                case "MiniExcel":
                    CompetitiveStreamWriters.WriteMiniExcel(fileStream);
                    break;
                case "ClosedXml":
                    CompetitiveStreamWriters.WriteClosedXml(fileStream);
                    break;
                default:
                    Console.Error.WriteLine($"未知情境：{scenario}");
                    return 1;
            }
        }

        stopwatch.Stop();
        long allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);
        long fileLength = new FileInfo(outputPath).Length;

        Console.WriteLine(FormattableString.Invariant(
            $"RESULT|{scenario}|{stopwatch.Elapsed.TotalMilliseconds}|{allocatedAfter - allocatedBefore}|{fileLength}"));
        return 0;
    }

    private static (string Scenario, double ElapsedMs, long AllocatedBytes, long FileLength, long PeakWorkingSetBytes) RunScenarioInChildProcess(string scenario)
    {
        string dllPath = typeof(CompetitiveStreamWriteManualRunner).Assembly.Location;
        string outputPath = Path.Combine(Path.GetTempPath(), $"odfkit-competitive-{scenario}-{Guid.NewGuid():N}.tmp");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(dllPath);
        startInfo.ArgumentList.Add("--run-single");
        startInfo.ArgumentList.Add(scenario);
        startInfo.ArgumentList.Add(outputPath);

        try
        {
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動子行程。");
            System.Threading.Tasks.Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            System.Threading.Tasks.Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            // Windows 在子行程結束後即拒絕查詢 PeakWorkingSet64（狀態變為 Exited 即失效），
            // 因此必須在行程仍存活期間輪詢並保留目前為止觀察到的最大值。
            long peakWorkingSetBytes = -1;
            while (!process.HasExited)
            {
                try
                {
                    process.Refresh();
                    peakWorkingSetBytes = System.Math.Max(peakWorkingSetBytes, process.PeakWorkingSet64);
                }
                catch (InvalidOperationException)
                {
                    // 行程可能在 Refresh 與讀取之間結束，忽略並於下一輪或結束後使用已知的最大值。
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
            long fileLength = long.Parse(parts[4], CultureInfo.InvariantCulture);

            return (scenario, elapsedMs, allocatedBytes, fileLength, peakWorkingSetBytes);
        }
        finally
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch (IOException)
            {
                // 暫存檔清理失敗不應中斷量測流程。
            }
        }
    }
}

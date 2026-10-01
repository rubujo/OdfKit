using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace OdfKit.Benchmarks;

/// <summary>
/// Standalone, single-shot timing runner for the ODT-versus-DOCX streaming comparison (write and read).
/// Each scenario runs in its own child process so peak working set reflects a single scenario. The read
/// scenarios read the files produced by the write scenarios and verify the content against the generator
/// with a <see cref="CompetitiveDocumentChecksum"/>, so a fast reader or writer cannot win by dropping or
/// corrupting data.
/// ODT 對 DOCX 串流對比（寫入與讀取）的獨立單次計時執行器。每個情境在各自的子行程執行，使峰值工作集只反映
/// 單一情境。讀取情境讀取寫入情境產生的檔案，並以 <see cref="CompetitiveDocumentChecksum"/> 對照產生器驗證內容，
/// 避免寫得快或讀得快卻遺失或破壞資料。
/// </summary>
internal static class CompetitiveDocumentManualRunner
{
    private const string OdtWrite = "OdtStreamWriter";
    private const string DocxWrite = "OpenXmlWriter";
    private const string OdtRead = "OdtStreamReader";
    private const string DocxRead = "OpenXmlReader";

    private sealed record ChildResult(
        string Scenario,
        double ElapsedMs,
        long AllocatedBytes,
        long PeakWorkingSetBytes,
        string Payload);

    /// <summary>
    /// Runs the write scenarios and then the read scenarios, and prints Markdown result tables.
    /// 先執行寫入情境再執行讀取情境，並印出 Markdown 結果表格。
    /// </summary>
    /// <returns>The process exit code (0 when every checksum matches). / 行程結束代碼（所有檢查碼相符時為 0）。</returns>
    internal static int RunOrchestrator()
    {
        Console.WriteLine(FormattableString.Invariant(
            $"文件對比手動計時模式：{CompetitiveDocumentData.NodeCount:N0} 個節點（每 {CompetitiveDocumentData.HeadingEvery} 個一個標題），每個情境於獨立子行程執行一次。"));

        string directory = Path.Combine(Path.GetTempPath(), $"odfkit-competitive-document-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string odtPath = Path.Combine(directory, "dataset.odt");
            string docxPath = Path.Combine(directory, "dataset.docx");

            Console.WriteLine("計算預期檢查碼（直接由產生器）…");
            CompetitiveDocumentChecksum expected = CompetitiveDocumentData.ComputeExpected();
            Console.WriteLine();

            var writes = new List<ChildResult>
            {
                RunChild(OdtWrite, odtPath),
                RunChild(DocxWrite, docxPath),
            };
            var reads = new List<(ChildResult Result, bool Matches)>();
            foreach ((string scenario, string path) in new[] { (OdtRead, odtPath), (DocxRead, docxPath) })
            {
                ChildResult result = RunChild(scenario, path);
                CompetitiveDocumentChecksum actual = CompetitiveDocumentChecksum.Parse(result.Payload);
                bool matches = actual == expected;
                if (!matches)
                {
                    Console.Error.WriteLine($"  預期：{expected.Serialize()}");
                    Console.Error.WriteLine($"  實際：{actual.Serialize()}");
                }

                reads.Add((result, matches));
            }

            Console.WriteLine();
            Console.WriteLine("寫入：");
            Console.WriteLine("| 情境 | 輸出格式 | 耗時 (ms) | 配置量 (MB) | 峰值工作集 (MB) | 輸出檔案大小 (MB) |");
            Console.WriteLine("|------|----------|-----------|-------------|------------------|--------------------|");
            foreach (ChildResult write in writes)
            {
                string format = write.Scenario == OdtWrite ? ".odt" : ".docx";
                string path = write.Scenario == OdtWrite ? odtPath : docxPath;
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {write.Scenario} | {format} | {write.ElapsedMs:N0} | {write.AllocatedBytes / 1024.0 / 1024.0:N1} | {FormatPeak(write.PeakWorkingSetBytes)} | {new FileInfo(path).Length / 1024.0 / 1024.0:N1} |"));
            }

            Console.WriteLine();
            Console.WriteLine("讀取：");
            Console.WriteLine("| 情境 | 輸入格式 | 耗時 (ms) | 配置量 (MB) | 峰值工作集 (MB) | 檢查碼 |");
            Console.WriteLine("|------|----------|-----------|-------------|------------------|--------|");
            foreach ((ChildResult read, bool matches) in reads)
            {
                string format = read.Scenario == OdtRead ? ".odt" : ".docx";
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {read.Scenario} | {format} | {read.ElapsedMs:N0} | {read.AllocatedBytes / 1024.0 / 1024.0:N1} | {FormatPeak(read.PeakWorkingSetBytes)} | {(matches ? "相符" : "不符")} |"));
            }

            if (reads.Exists(item => !item.Matches))
            {
                Console.Error.WriteLine("檢查碼不符：至少一個寫入器或讀取器產生的內容與產生器不一致，量測結果無效。");
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
    /// Runs a single named scenario in-process and prints a machine-readable result line.
    /// 在目前行程中執行單一指定情境，並印出機器可讀的結果行。
    /// </summary>
    /// <param name="scenario">The scenario name. / 情境名稱。</param>
    /// <param name="path">The output path for write scenarios, or the input path for read scenarios. / 寫入情境的輸出路徑，或讀取情境的輸入路徑。</param>
    /// <returns>The process exit code (0 on success). / 行程結束代碼（成功時為 0）。</returns>
    internal static int RunSingleScenario(string scenario, string path)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        string payload;
        switch (scenario)
        {
            case OdtWrite:
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    CompetitiveDocumentStreams.WriteOdt(stream);
                }

                payload = "written";
                break;
            case DocxWrite:
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                {
                    CompetitiveDocumentStreams.WriteDocx(stream);
                }

                payload = "written";
                break;
            case OdtRead:
                payload = CompetitiveDocumentStreams.ReadOdt(path).Serialize();
                break;
            case DocxRead:
                payload = CompetitiveDocumentStreams.ReadDocx(path).Serialize();
                break;
            default:
                Console.Error.WriteLine($"未知情境：{scenario}");
                return 1;
        }

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        Console.WriteLine(FormattableString.Invariant(
            $"RESULT|{scenario}|{stopwatch.Elapsed.TotalMilliseconds}|{allocated}|{payload}"));
        return 0;
    }

    private static string FormatPeak(long bytes) =>
        bytes >= 0
            ? (bytes / 1024.0 / 1024.0).ToString("N1", CultureInfo.InvariantCulture)
            : "n/a";

    private static ChildResult RunChild(string scenario, string path)
    {
        Console.WriteLine($"執行中：{scenario}…");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(typeof(CompetitiveDocumentManualRunner).Assembly.Location);
        startInfo.ArgumentList.Add("--run-document-single");
        startInfo.ArgumentList.Add(scenario);
        startInfo.ArgumentList.Add(path);

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
        ChildResult result = new(
            scenario,
            double.Parse(parts[2], CultureInfo.InvariantCulture),
            long.Parse(parts[3], CultureInfo.InvariantCulture),
            peakWorkingSetBytes,
            parts[4]);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  耗時：{result.ElapsedMs:N0} ms，配置量：{result.AllocatedBytes / 1024.0 / 1024.0:N1} MB，峰值工作集：{FormatPeak(result.PeakWorkingSetBytes)}"));
        return result;
    }
}

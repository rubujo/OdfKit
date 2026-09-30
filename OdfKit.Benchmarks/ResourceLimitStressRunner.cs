using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// 資源限制壓力測試：把 docs/security-limits.md 記錄的限制變成可執行的檢查。
/// BenchmarkDotNet 無法量測「壞了就整個處理程序崩潰」的情境（堆疊溢位、記憶體耗盡），
/// 因此每個情境都在獨立子處理程序執行；子處理程序崩潰即視為失敗。
/// 另外對「資料量放大 4 倍」的情境檢查縮放比：耗時比值約 4 為線性，約 16 為二次方。
/// 絕對耗時受機器影響，縮放比則不受影響，因此能抓到複雜度回歸（例如逐列建立工作表的全表重掃）。
/// </summary>
internal static class ResourceLimitStressRunner
{
    /// <summary>
    /// 資料量放大 4 倍時可接受的耗時比值上限（線性約 4，二次方約 16）。
    /// 比較最小與最大規模而非逐步加倍，可避免單步的 GC 與計時雜訊造成誤報。
    /// </summary>
    internal const double MaxQuadrupleRatio = 8.0;

    /// <summary>低於此耗時（毫秒）的量測太接近計時雜訊，不做縮放比判斷。</summary>
    internal const double MinMeasurableMilliseconds = 40;

    private static readonly int[] s_rowCounts = [8_000, 16_000, 32_000];

    // 修正前實測會空轉或極慢的公式（見 FormulaIterationLimitTests）。
    private static readonly string[] s_largeArgumentFormulas =
    [
        "of:=CRITBINOM(2000000000;0.5;0.5)",
        "of:=MULTINOMIAL(1000000000;1000000000)",
        "of:=FACT(1000000000)",
        "of:=FACTDOUBLE(1000000000)",
        "of:=COMBIN(1000000000;500000000)",
        "of:=HYPGEOMDIST(1000000000;2000000000;1000000000;3000000000;1)",
        "of:=POISSON(1000000000;1000000000;1)",
        "of:=DB(2147483647;2147483647;2147483647;2147483647)",
    ];

    private sealed record ChildResult(bool Survived, int ExitCode, double Milliseconds, string Outcome);

    internal static int RunOrchestrator()
    {
        var failures = new List<string>();

        Console.WriteLine("== 縮放比：逐列建立工作表 ==");
        var timings = new List<(int Rows, double Milliseconds)>();
        foreach (int rows in s_rowCounts)
        {
            // 取兩次量測的最小值，降低共享 runner 的雜訊。
            ChildResult best = Enumerable.Range(0, 2)
                .Select(_ => RunChild("rows-sequential", rows.ToString(CultureInfo.InvariantCulture)))
                .OrderBy(result => result.Milliseconds)
                .First();
            if (!best.Survived || best.Outcome != "ok")
            {
                failures.Add($"rows-sequential {rows}：{Describe(best)}");
                continue;
            }

            Console.WriteLine($"  rows={rows,6}  {best.Milliseconds,8:F0} ms");
            timings.Add((rows, best.Milliseconds));
        }

        if (timings.Count == s_rowCounts.Length && timings[0].Milliseconds >= MinMeasurableMilliseconds)
        {
            double ratio = timings[^1].Milliseconds / timings[0].Milliseconds;
            Console.WriteLine(
                $"  {timings[0].Rows} → {timings[^1].Rows} 列（{timings[^1].Rows / timings[0].Rows} 倍）耗時比 {ratio:F2}，上限 {MaxQuadrupleRatio:F1}");
            if (ratio > MaxQuadrupleRatio)
            {
                failures.Add(
                    $"rows-sequential：資料量放大 {timings[^1].Rows / timings[0].Rows} 倍，耗時放大 {ratio:F2} 倍，"
                    + $"超過 {MaxQuadrupleRatio:F1}，疑似二次方成本。");
            }
        }

        Console.WriteLine("== 堆疊安全：運算子連鎖公式（256 KB 與 128 KB 執行緒）==");
        foreach (int stackKb in new[] { 256, 128 })
        {
            // 4,000 項接近 4,096 上限；16,000 項超過上限，必須被乾淨拒絕。
            foreach (int terms in new[] { 4_000, 16_000 })
            {
                ChildResult result = RunChild(
                    "chain-stack",
                    terms.ToString(CultureInfo.InvariantCulture),
                    stackKb.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine($"  terms={terms,6} stack={stackKb}KB  {Describe(result)}");
                if (!result.Survived)
                {
                    failures.Add($"chain-stack terms={terms} stack={stackKb}KB：{Describe(result)}");
                }
            }
        }

        Console.WriteLine("== 超長前置運算子鏈 ==");
        ChildResult unary = RunChild("unary-chain", "1000000");
        Console.WriteLine($"  length=1000000  {Describe(unary)}");
        if (!unary.Survived || unary.Outcome != "rejected")
        {
            failures.Add($"unary-chain：{Describe(unary)}（預期 rejected）");
        }

        Console.WriteLine("== 以引數為迴圈上限的公式 ==");
        ChildResult largeArguments = RunChild("large-args");
        Console.WriteLine($"  {Describe(largeArguments)}");
        if (!largeArguments.Survived || largeArguments.Outcome != "ok")
        {
            failures.Add($"large-args：{Describe(largeArguments)}");
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("資源限制壓力測試通過。");
            return 0;
        }

        Console.Error.WriteLine("資源限制壓力測試失敗：");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine("  - " + failure);
        }

        return 1;
    }

    /// <summary>子處理程序進入點。輸出一行 <c>RESULT ms outcome</c>；非預期例外回傳非零。</summary>
    internal static int RunSingle(string scenario, string[] arguments)
    {
        var stopwatch = Stopwatch.StartNew();
        string outcome;
        switch (scenario)
        {
            case "rows-sequential":
                outcome = SequentialRows(int.Parse(arguments[0], CultureInfo.InvariantCulture));
                break;
            case "chain-stack":
                outcome = ChainOnSmallStack(
                    int.Parse(arguments[0], CultureInfo.InvariantCulture),
                    int.Parse(arguments[1], CultureInfo.InvariantCulture));
                break;
            case "unary-chain":
                outcome = UnaryChain(int.Parse(arguments[0], CultureInfo.InvariantCulture));
                break;
            case "large-args":
                outcome = LargeArguments();
                break;
            default:
                Console.Error.WriteLine("未知的情境：" + scenario);
                return 2;
        }

        stopwatch.Stop();
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"RESULT {stopwatch.Elapsed.TotalMilliseconds:F3} {outcome}"));
        return outcome.StartsWith("unexpected:", StringComparison.Ordinal) ? 2 : 0;
    }

    private static string SequentialRows(int rows)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        for (int row = 0; row < rows; row++)
        {
            sheet.GetCell(row, 0).CellValue = row;
        }

        return "ok";
    }

    private static string ChainOnSmallStack(int terms, int stackKb)
    {
        string outcome = "unexpected:未執行";
        var thread = new Thread(
            () => outcome = EvaluateFormula("of:=1" + string.Concat(Enumerable.Repeat("+1", terms))),
            stackKb * 1024);
        thread.Start();
        thread.Join();
        return outcome;
    }

    private static string UnaryChain(int length)
        => EvaluateFormula("of:=" + new string('-', length) + "1");

    private static string LargeArguments()
    {
        foreach (string formula in s_largeArgumentFormulas)
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create();
            OdfTableSheet sheet = document.AddSheet("D");
            sheet.Cells["A1"].SetFormula(formula, 0d);
            var stopwatch = Stopwatch.StartNew();
            document.EvaluateFormulas();
            // 上限是「不空轉數十億次」；慢速 runner 允許 10 秒，遠低於修正前的數十秒以上。
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
            {
                return $"unexpected:{formula} 耗時 {stopwatch.Elapsed.TotalSeconds:F1} 秒";
            }
        }

        return "ok";
    }

    /// <summary>求值單一公式：成功為 ok；被上限乾淨拒絕為 rejected；其餘例外為非預期。</summary>
    private static string EvaluateFormula(string formula)
    {
        try
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Create();
            OdfTableSheet sheet = document.AddSheet("D");
            sheet.Cells["A1"].SetFormula(formula, 0d);
            document.EvaluateFormulas();
            return "ok";
        }
        catch (Exception exception) when (exception is InvalidOperationException or InsufficientExecutionStackException)
        {
            // OdfFormulaEvaluationException 繼承自 InvalidOperationException。
            return "rejected";
        }
        catch (Exception exception)
        {
            return "unexpected:" + exception.GetType().Name;
        }
    }

    private static ChildResult RunChild(string scenario, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add(typeof(ResourceLimitStressRunner).Assembly.Location);
        startInfo.ArgumentList.Add("--stress-single");
        startInfo.ArgumentList.Add(scenario);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("無法啟動子處理程序：" + scenario);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        string? line = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(candidate => candidate.Trim())
            .LastOrDefault(candidate => candidate.StartsWith("RESULT ", StringComparison.Ordinal));
        if (line is null)
        {
            return new ChildResult(false, process.ExitCode, 0, "process-crashed");
        }

        string[] parts = line.Split(' ', 3);
        double milliseconds = double.Parse(parts[1], CultureInfo.InvariantCulture);
        string outcome = parts.Length > 2 ? parts[2] : string.Empty;
        return new ChildResult(process.ExitCode == 0, process.ExitCode, milliseconds, outcome);
    }

    private static string Describe(ChildResult result)
        => result.Outcome == "process-crashed"
            ? $"處理程序崩潰（結束碼 {result.ExitCode}）"
            : $"{result.Outcome}（{result.Milliseconds:F0} ms）";
}

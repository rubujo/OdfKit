using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Running;

namespace OdfKit.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        return BenchmarkProcessGuard.Run(
            () =>
            {
                BenchmarkProcessGuard.SuppressWindowsErrorDialogs();
                return Run(args);
            },
            Console.Error);
    }

    private static int Run(string[] args)
    {
        // 手動計時模式（用於 1,000,000 列跨套件對比情境，見 CompetitiveStreamWriteManualRunner 說明）。
        if (args.Length > 0 && string.Equals(args[0], "--manual-competitive", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveStreamWriteManualRunner.RunOrchestrator();
        }

        if (args.Length >= 3 && string.Equals(args[0], "--run-single", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveStreamWriteManualRunner.RunSingleScenario(args[1], args[2]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--manual-competitive-read", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveStreamReadManualRunner.RunOrchestrator();
        }

        if (args.Length >= 3 && string.Equals(args[0], "--run-read-single", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveStreamReadManualRunner.RunSingleScenario(args[1], args[2]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--manual-competitive-document", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveDocumentManualRunner.RunOrchestrator();
        }

        if (args.Length >= 3 && string.Equals(args[0], "--run-document-single", StringComparison.OrdinalIgnoreCase))
        {
            return CompetitiveDocumentManualRunner.RunSingleScenario(args[1], args[2]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--stress", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceLimitStressRunner.RunOrchestrator();
        }

        if (args.Length >= 2 && string.Equals(args[0], "--stress-single", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceLimitStressRunner.RunSingle(args[1], args[2..]);
        }

        if (args.Length > 0 && string.Equals(args[0], "--manual-standard", StringComparison.OrdinalIgnoreCase))
        {
            return StandardPerformanceManualRunner.RunOrchestrator();
        }

        if (args.Length >= 3 && string.Equals(args[0], "--run-standard-single", StringComparison.OrdinalIgnoreCase))
        {
            return StandardPerformanceManualRunner.RunSingleScenario(args[1], args[2]);
        }

        return RunBenchmarks(BenchmarkProcessGuard.EnsureUniqueArtifactsPath(args, CreateDefaultArtifactsPath));
    }

    private static int RunBenchmarks(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        bool failed = summaries.Any(summary =>
            summary.HasCriticalValidationErrors ||
            summary.Reports.Any(report => !report.Success));
        return failed ? BenchmarkProcessGuard.FailureExitCode : 0;
    }

    private static string CreateDefaultArtifactsPath()
    {
        string runName = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        return Path.GetFullPath(Path.Combine("BenchmarkDotNet.Artifacts", "runs", runName));
    }
}

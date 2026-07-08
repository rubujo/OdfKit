using System;
using BenchmarkDotNet.Running;

namespace OdfKit.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
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

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OdfKit.Benchmarks;

internal static class BenchmarkProcessGuard
{
    internal const int FailureExitCode = 1;

    private const uint SemFailCriticalErrors = 0x0001;
    private const uint SemNoGpFaultErrorBox = 0x0002;
    private const uint SemNoOpenFileErrorBox = 0x8000;

    internal static int Run(Func<int> action, TextWriter errorWriter)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(errorWriter);

        try
        {
            return action();
        }
        catch (Exception exception)
        {
            errorWriter.WriteLine("OdfKit.Benchmarks 執行失敗。");
            errorWriter.WriteLine(exception);
            return FailureExitCode;
        }
    }

    internal static string[] EnsureUniqueArtifactsPath(string[] args, Func<string> pathFactory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(pathFactory);

        if (args.Any(argument =>
            string.Equals(argument, "--artifacts", StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith("--artifacts=", StringComparison.OrdinalIgnoreCase)))
        {
            return args;
        }

        string[] preparedArgs = new string[args.Length + 2];
        args.CopyTo(preparedArgs, 0);
        preparedArgs[^2] = "--artifacts";
        preparedArgs[^1] = pathFactory();
        return preparedArgs;
    }

    internal static void SuppressWindowsErrorDialogs()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        uint currentMode = GetErrorMode();
        _ = SetErrorMode(currentMode | SemFailCriticalErrors | SemNoGpFaultErrorBox | SemNoOpenFileErrorBox);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}

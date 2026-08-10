using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;

namespace OdfKit.Benchmarks;

internal static class StandardPerformanceManualRunner
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private static readonly string[] s_scenarios =
    [
        "OdsStreamWrite", "OdsStreamRead", "OdsDomRoundTrip",
        "OdtStreamWrite", "OdtStreamRead", "OdtDomRoundTrip",
        "OdpStructureWrite", "OdpStructureRead", "OdpMediaRoundTrip",
    ];

    internal static int RunOrchestrator()
    {
        string dllPath = typeof(StandardPerformanceManualRunner).Assembly.Location;
        string resultRoot = Path.Combine(Path.GetTempPath(), "odfkit-standard-performance", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(resultRoot);
        try
        {
            var results = new List<StandardPerformanceResult>();
            foreach (string scenario in s_scenarios)
            {
                string resultPath = Path.Combine(resultRoot, scenario + ".json");
                var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
                startInfo.ArgumentList.Add(dllPath);
                startInfo.ArgumentList.Add("--run-standard-single");
                startInfo.ArgumentList.Add(scenario);
                startInfo.ArgumentList.Add(resultPath);
                using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start {scenario}.");
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return process.ExitCode;
                }

                StandardPerformanceResult result = JsonSerializer.Deserialize<StandardPerformanceResult>(File.ReadAllText(resultPath))
                    ?? throw new InvalidOperationException($"Unable to read {scenario} result.");
                results.Add(result);
            }

            Console.WriteLine(JsonSerializer.Serialize(results, s_indentedJsonOptions));
            return 0;
        }
        finally
        {
            Directory.Delete(resultRoot, recursive: true);
        }
    }

    internal static int RunSingleScenario(string scenario, string resultPath)
    {
        byte[]? preparedInput = PrepareInput(scenario);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        (byte[] bytes, ulong checksum) = RunScenario(scenario, preparedInput);
        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        long measuredPeakWorkingSet = process.PeakWorkingSet64;
        if (checksum == 0)
        {
            checksum = CalculateOutputChecksum(scenario, bytes);
        }
        (long packageBytes, long xmlBytes) = StandardPerformanceWorkloads.GetPackageSizes(bytes);
        var result = new StandardPerformanceResult(1, scenario, stopwatch.Elapsed.TotalMilliseconds, allocated,
            measuredPeakWorkingSet, packageBytes, xmlBytes, checksum, Environment.Version.ToString(),
            Environment.OSVersion.ToString(), Environment.ProcessorCount);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result));
        return 0;
    }

    private static byte[]? PrepareInput(string scenario) => scenario switch
    {
        "OdsStreamRead" => StandardPerformanceWorkloads.CreateStreamingOds(StandardPerformanceWorkloads.StandardOdsReadRowCount),
        "OdsDomRoundTrip" => StandardPerformanceWorkloads.CreateComplexOds(2_000),
        "OdtStreamRead" => StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount),
        "OdtDomRoundTrip" => StandardPerformanceWorkloads.CreateComplexOdt(20_000),
        "OdpStructureRead" => StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpStructureSlideCount, includeMedia: false),
        "OdpMediaRoundTrip" => StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpMediaSlideCount, includeMedia: true),
        _ => null,
    };

    private static (byte[] Bytes, ulong Checksum) RunScenario(string scenario, byte[]? preparedInput) => scenario switch
    {
        "OdsStreamWrite" => (StandardPerformanceWorkloads.CreateStreamingOds(StandardPerformanceWorkloads.StandardOdsRowCount), 0),
        "OdsStreamRead" => ReadOds(RequireInput(preparedInput)),
        "OdsDomRoundTrip" => RoundTripOds(RequireInput(preparedInput)),
        "OdtStreamWrite" => (StandardPerformanceWorkloads.CreateStreamingOdt(StandardPerformanceWorkloads.StandardOdtNodeCount), 0),
        "OdtStreamRead" => ReadOdt(RequireInput(preparedInput)),
        "OdtDomRoundTrip" => RoundTripOdt(RequireInput(preparedInput)),
        "OdpStructureWrite" => (StandardPerformanceWorkloads.CreateOdp(StandardPerformanceWorkloads.StandardOdpStructureSlideCount, includeMedia: false), 0),
        "OdpStructureRead" => ReadOdp(RequireInput(preparedInput)),
        "OdpMediaRoundTrip" => RoundTripOdp(RequireInput(preparedInput)),
        _ => throw new ArgumentException($"Unknown standard performance scenario: {scenario}", nameof(scenario)),
    };

    private static (byte[], ulong) ReadOds(byte[] bytes)
    {
        return (bytes, StandardPerformanceWorkloads.ChecksumStreamingOds(bytes));
    }

    private static (byte[], ulong) RoundTripOds(byte[] source)
    {
        using var input = new MemoryStream(source, writable: false);
        using SpreadsheetDocument document = SpreadsheetDocument.Load(input, "complex.ods");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        byte[] bytes = output.ToArray();
        return (bytes, 0);
    }

    private static (byte[], ulong) ReadOdt(byte[] bytes)
    {
        return (bytes, StandardPerformanceWorkloads.ChecksumStreamingOdt(bytes));
    }

    private static (byte[], ulong) RoundTripOdt(byte[] source)
    {
        using var input = new MemoryStream(source, writable: false);
        using TextDocument document = TextDocument.Load(input, "complex.odt");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        byte[] bytes = output.ToArray();
        return (bytes, 0);
    }

    private static (byte[], ulong) ReadOdp(byte[] bytes)
    {
        return (bytes, StandardPerformanceWorkloads.ChecksumOdp(bytes));
    }

    private static (byte[], ulong) RoundTripOdp(byte[] source)
    {
        using var input = new MemoryStream(source, writable: false);
        using PresentationDocument document = PresentationDocument.Load(input, "media.odp");
        using var output = new MemoryStream();
        document.SaveToStream(output);
        byte[] bytes = output.ToArray();
        return (bytes, 0);
    }

    private static ulong CalculateOutputChecksum(string scenario, byte[] bytes)
    {
        if (scenario.StartsWith("Ods", StringComparison.Ordinal))
        {
            return scenario == "OdsStreamWrite"
                ? StandardPerformanceWorkloads.ChecksumOdsXml(bytes)
                : StandardPerformanceWorkloads.ChecksumComplexOds(bytes);
        }

        return scenario.StartsWith("Odt", StringComparison.Ordinal)
            ? StandardPerformanceWorkloads.ChecksumStreamingOdt(bytes)
            : StandardPerformanceWorkloads.ChecksumOdp(bytes);
    }

    private static byte[] RequireInput(byte[]? input) => input ?? throw new InvalidOperationException("Prepared benchmark input is missing.");
}

internal sealed record StandardPerformanceResult(int SchemaVersion, string Scenario, double ElapsedMilliseconds,
    long AllocatedBytes, long PeakWorkingSetBytes, long PackageBytes, long XmlBytes, ulong Checksum,
    string RuntimeVersion, string OperatingSystem, int ProcessorCount);

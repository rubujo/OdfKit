using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace OdfKit.Benchmarks;

/// <summary>
/// Cross-package reference benchmark comparing <see cref="OdfKit.Spreadsheet.OdsStreamReader"/> against
/// MiniExcel's streaming reader and ClosedXML's DOM loader for a 1,000,000-row × 10-column mixed-type dataset.
/// 跨套件參考基準測試，比較 <see cref="OdfKit.Spreadsheet.OdsStreamReader"/> 與 MiniExcel 串流讀取器、
/// ClosedXML DOM 載入器在一百萬列 × 十欄混合型別資料集下的表現。
/// </summary>
/// <remarks>
/// This is a cross-format reference comparison (ODS vs. XLSX), not a same-format contest; see
/// docs/performance-comparison.md. The input files are written once in <see cref="GlobalSetup"/> and are
/// not part of the measurement. Because each iteration reads 1,000,000 rows, this class opts into
/// <see cref="RunStrategy.Monitoring"/> instead of BenchmarkDotNet's default many-iteration statistical job.
/// 此為跨格式參考對比（ODS 對 XLSX），而非同格式對決；請見 docs/performance-comparison.md。輸入檔於
/// <see cref="GlobalSetup"/> 寫入一次，不計入量測。由於每次迭代都會讀取一百萬列，此類別選用
/// <see cref="RunStrategy.Monitoring"/>，而非 BenchmarkDotNet 預設的多次迭代統計工作。
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 0, iterationCount: 3)]
public class CompetitiveStreamReadBenchmarks
{
    private string _directory = null!;
    private string _odsPath = null!;
    private string _xlsxPath = null!;

    /// <summary>
    /// Writes the .ods and .xlsx input files once, outside the measured region.
    /// 於量測範圍之外，一次性寫出 .ods 與 .xlsx 輸入檔。
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"odfkit-competitive-read-bdn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _odsPath = Path.Combine(_directory, "dataset.ods");
        _xlsxPath = Path.Combine(_directory, "dataset.xlsx");
        using (FileStream ods = File.Create(_odsPath))
        {
            CompetitiveStreamWriters.WriteOdsStreamWriter(ods);
        }

        using (FileStream xlsx = File.Create(_xlsxPath))
        {
            CompetitiveStreamWriters.WriteMiniExcel(xlsx);
        }
    }

    /// <summary>
    /// Deletes the input files.
    /// 刪除輸入檔。
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 暫存檔清理失敗不應中斷量測流程。
        }
    }

    /// <summary>
    /// Reads 1,000,000 mixed-type rows using OdfKit's <c>OdsStreamReader</c>.
    /// 使用 OdfKit 的 <c>OdsStreamReader</c> 讀取一百萬列混合型別資料。
    /// </summary>
    /// <returns>The number of rows read. / 讀到的列數。</returns>
    [Benchmark(Baseline = true)]
    public long OdsStreamReader_ReadOneMillionRows() => CompetitiveStreamReaders.ReadOdsStreamReader(_odsPath).Rows;

    /// <summary>
    /// Reads 1,000,000 mixed-type rows using MiniExcel's streaming <c>Query</c> API.
    /// 使用 MiniExcel 串流式 <c>Query</c> API 讀取一百萬列混合型別資料。
    /// </summary>
    /// <returns>The number of rows read. / 讀到的列數。</returns>
    [Benchmark]
    public long MiniExcel_ReadOneMillionRows() => CompetitiveStreamReaders.ReadMiniExcel(_xlsxPath).Rows;

    /// <summary>
    /// Reads 1,000,000 mixed-type rows using ClosedXML's in-memory DOM loader (non-streaming control group).
    /// 使用 ClosedXML 記憶體內 DOM 載入器讀取一百萬列混合型別資料（非串流對照組）。
    /// </summary>
    /// <returns>The number of rows read. / 讀到的列數。</returns>
    [Benchmark]
    public long ClosedXml_ReadOneMillionRows() => CompetitiveStreamReaders.ReadClosedXml(_xlsxPath).Rows;
}

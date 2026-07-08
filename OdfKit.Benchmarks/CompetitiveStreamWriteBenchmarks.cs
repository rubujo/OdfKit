using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace OdfKit.Benchmarks;

/// <summary>
/// Cross-package reference benchmark comparing <see cref="OdfKit.Spreadsheet.OdsStreamWriter"/>
/// against MiniExcel's streaming writer and ClosedXML's DOM writer for a 1,000,000-row × 10-column
/// mixed-type dataset.
/// 跨套件參考基準測試，比較 <see cref="OdfKit.Spreadsheet.OdsStreamWriter"/> 與 MiniExcel 串流寫入器、
/// ClosedXML DOM 寫入器在一百萬列 × 十欄混合型別資料集下的表現。
/// </summary>
/// <remarks>
/// This is a cross-format reference comparison (ODS vs. XLSX), not a same-format contest; see
/// docs/performance-comparison.md for the full methodology, licensing rationale, and result
/// interpretation. Because each iteration writes 1,000,000 rows, this class opts into
/// <see cref="RunStrategy.Monitoring"/> with a single warmup-free measured iteration per invocation
/// instead of BenchmarkDotNet's default many-iteration statistical job, to keep a full run within a
/// practical wall-clock budget; run <c>eng/Benchmark-Competitive.ps1</c> for the documented
/// reproduction command.
/// 此為跨格式參考對比（ODS 對 XLSX），而非同格式對決；完整方法論、授權裁定與結果解讀請見
/// docs/performance-comparison.md。由於每次迭代都會寫入一百萬列，此類別選用
/// <see cref="RunStrategy.Monitoring"/>，每次呼叫僅量測一次且不預熱，而非 BenchmarkDotNet
/// 預設的多次迭代統計工作，以將完整執行時間控制在合理範圍內；重現指令請見
/// <c>eng/Benchmark-Competitive.ps1</c>。
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 0, iterationCount: 3)]
public class CompetitiveStreamWriteBenchmarks
{
    private MemoryStream _outputStream = null!;

    /// <summary>
    /// Resets the reusable output buffer before each measured invocation.
    /// 在每次量測呼叫前重設可重複使用的輸出緩衝區。
    /// </summary>
    [IterationSetup]
    public void IterationSetup() => _outputStream = new MemoryStream();

    /// <summary>
    /// Writes 1,000,000 mixed-type rows using OdfKit's <c>OdsStreamWriter</c>.
    /// 使用 OdfKit 的 <c>OdsStreamWriter</c> 寫入一百萬列混合型別資料。
    /// </summary>
    /// <returns>The produced .ods byte length. / 產生之 .ods 位元組長度。</returns>
    [Benchmark(Baseline = true)]
    public long OdsStreamWriter_WriteOneMillionRows()
    {
        CompetitiveStreamWriters.WriteOdsStreamWriter(_outputStream);
        return _outputStream.Length;
    }

    /// <summary>
    /// Writes 1,000,000 mixed-type rows using MiniExcel's streaming <c>SaveAs</c> API.
    /// 使用 MiniExcel 串流式 <c>SaveAs</c> API 寫入一百萬列混合型別資料。
    /// </summary>
    /// <returns>The produced .xlsx byte length. / 產生之 .xlsx 位元組長度。</returns>
    [Benchmark]
    public long MiniExcel_WriteOneMillionRows()
    {
        CompetitiveStreamWriters.WriteMiniExcel(_outputStream);
        return _outputStream.Length;
    }

    /// <summary>
    /// Writes 1,000,000 mixed-type rows using ClosedXML's in-memory DOM writer (non-streaming
    /// control group).
    /// 使用 ClosedXML 記憶體內 DOM 寫入器寫入一百萬列混合型別資料（非串流對照組）。
    /// </summary>
    /// <returns>The produced .xlsx byte length. / 產生之 .xlsx 位元組長度。</returns>
    [Benchmark]
    public long ClosedXml_WriteOneMillionRows()
    {
        CompetitiveStreamWriters.WriteClosedXml(_outputStream);
        return _outputStream.Length;
    }
}

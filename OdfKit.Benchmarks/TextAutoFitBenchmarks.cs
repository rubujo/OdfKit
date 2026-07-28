using BenchmarkDotNet.Attributes;
using OdfKit.Extensions.Imaging;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

namespace OdfKit.Benchmarks;

/// <summary>
/// 量測批次欄寬與列高在 Fast／Precise 模式下的時間與配置量。
/// </summary>
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet owns the lifecycle and invokes GlobalCleanup.")]
public class TextAutoFitBenchmarks
{
    private SpreadsheetDocument _document = null!;
    private OdfTableSheet _sheet = null!;
    private OdfTextLayoutSession _preciseSession = null!;
    private OdfAutoFitOptions _fastOptions = null!;
    private OdfAutoFitOptions _preciseOptions = null!;

    /// <summary>
    /// 取得或設定基準資料列數。
    /// </summary>
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    /// <summary>
    /// 建立具重複值與 Unicode 內容的固定基準工作表。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = SpreadsheetDocument.Create();
        _sheet = _document.Worksheets.Add("Layout");
        _sheet.SetValues(
            new OdfCellAddress(0, 0),
            CreateRows(RowCount));
        _preciseSession = new OdfTextLayoutSession(_document.FontContext);
        _fastOptions = new OdfAutoFitOptions();
        _preciseOptions = new OdfAutoFitOptions
        {
            Mode = OdfAutoFitMode.Precise,
            TextMeasurer = _preciseSession
        };
    }

    /// <summary>
    /// 釋放文件與精確字型量測工作階段。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _preciseSession.Dispose();
        _document.Dispose();
    }

    /// <summary>
    /// 量測核心 Fast 批次欄寬掃描。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int FastColumns()
    {
        IReadOnlyDictionary<int, OdfLength> widths =
            _sheet.AutoFitColumnWidths([0, 1, 2, 3], _fastOptions);
        return widths.Count;
    }

    /// <summary>
    /// 量測具工作階段快取的 Precise 批次欄寬掃描。
    /// </summary>
    [Benchmark]
    public int PreciseColumns()
    {
        IReadOnlyDictionary<int, OdfLength> widths =
            _sheet.AutoFitColumnWidths([0, 1, 2, 3], _preciseOptions);
        return widths.Count;
    }

    /// <summary>
    /// 量測欄寬確定後的 Fast 批次列高。
    /// </summary>
    [Benchmark]
    public int FastRows()
    {
        IReadOnlyDictionary<int, OdfLength> heights =
            _sheet.AutoFitRowHeights(
                System.Linq.Enumerable.Range(0, RowCount),
                _fastOptions);
        return heights.Count;
    }

    private static IEnumerable<IEnumerable<object?>> CreateRows(int rowCount)
    {
        for (int row = 0; row < rowCount; row++)
        {
            yield return
            [
                $"Item {row % 100}",
                $"臺灣資料 {row % 50}",
                "😀 emoji",
                "wrapped\ntext"
            ];
        }
    }
}
